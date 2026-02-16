// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Nodes;

namespace Rdn.Schema
{
    internal sealed class RdnSchema
    {
        internal const string RefPropertyName = "$ref";
        internal const string CommentPropertyName = "$comment";
        internal const string TypePropertyName = "type";
        internal const string FormatPropertyName = "format";
        internal const string PatternPropertyName = "pattern";
        internal const string PropertiesPropertyName = "properties";
        internal const string RequiredPropertyName = "required";
        internal const string ItemsPropertyName = "items";
        internal const string AdditionalPropertiesPropertyName = "additionalProperties";
        internal const string EnumPropertyName = "enum";
        internal const string NotPropertyName = "not";
        internal const string AnyOfPropertyName = "anyOf";
        internal const string ConstPropertyName = "const";
        internal const string DefaultPropertyName = "default";
        internal const string MinLengthPropertyName = "minLength";
        internal const string MaxLengthPropertyName = "maxLength";

        public static RdnSchema CreateFalseSchema() => new(false);
        public static RdnSchema CreateTrueSchema() => new(true);

        public RdnSchema() { }
        private RdnSchema(bool trueOrFalse) { _trueOrFalse = trueOrFalse; }

        public bool IsTrue => _trueOrFalse is true;
        public bool IsFalse => _trueOrFalse is false;

        /// <summary>
        /// Per the RDN schema core specification section 4.3
        /// (https://rdn-schema.org/draft/2020-12/rdn-schema-core#name-rdn-schema-documents)
        /// A RDN schema must either be an object or a boolean.
        /// We represent false and true schemas using this flag.
        /// It is not possible to specify keywords in boolean schemas.
        /// </summary>
        private readonly bool? _trueOrFalse;

        public string? Ref { get => _ref; set { VerifyMutable(); _ref = value; } }
        private string? _ref;

        public string? Comment { get => _comment; set { VerifyMutable(); _comment = value; } }
        private string? _comment;

        public RdnSchemaType Type { get => _type; set { VerifyMutable(); _type = value; } }
        private RdnSchemaType _type = RdnSchemaType.Any;

        public string? Format { get => _format; set { VerifyMutable(); _format = value; } }
        private string? _format;

        public string? Pattern { get => _pattern; set { VerifyMutable(); _pattern = value; } }
        private string? _pattern;

        public RdnNode? Constant { get => _constant; set { VerifyMutable(); _constant = value; } }
        private RdnNode? _constant;

        public List<KeyValuePair<string, RdnSchema>>? Properties { get => _properties; set { VerifyMutable(); _properties = value; } }
        private List<KeyValuePair<string, RdnSchema>>? _properties;

        public List<string>? Required { get => _required; set { VerifyMutable(); _required = value; } }
        private List<string>? _required;

        public RdnSchema? Items { get => _items; set { VerifyMutable(); _items = value; } }
        private RdnSchema? _items;

        public RdnSchema? AdditionalProperties { get => _additionalProperties; set { VerifyMutable(); _additionalProperties = value; } }
        private RdnSchema? _additionalProperties;

        public RdnArray? Enum { get => _enum; set { VerifyMutable(); _enum = value; } }
        private RdnArray? _enum;

        public RdnSchema? Not { get => _not; set { VerifyMutable(); _not = value; } }
        private RdnSchema? _not;

        public List<RdnSchema>? AnyOf { get => _anyOf; set { VerifyMutable(); _anyOf = value; } }
        private List<RdnSchema>? _anyOf;

        public bool HasDefaultValue { get => _hasDefaultValue; set { VerifyMutable(); _hasDefaultValue = value; } }
        private bool _hasDefaultValue;

        public RdnNode? DefaultValue { get => _defaultValue; set { VerifyMutable(); _defaultValue = value; } }
        private RdnNode? _defaultValue;

        public int? MinLength { get => _minLength; set { VerifyMutable(); _minLength = value; } }
        private int? _minLength;

        public int? MaxLength { get => _maxLength; set { VerifyMutable(); _maxLength = value; } }
        private int? _maxLength;

        public RdnSchemaExporterContext? ExporterContext { get; set; }

        public int KeywordCount
        {
            get
            {
                if (_trueOrFalse != null)
                {
                    // Boolean schemas admit no keywords
                    return 0;
                }

                int count = 0;
                Count(Ref != null);
                Count(Comment != null);
                Count(Type != RdnSchemaType.Any);
                Count(Format != null);
                Count(Pattern != null);
                Count(Constant != null);
                Count(Properties != null);
                Count(Required != null);
                Count(Items != null);
                Count(AdditionalProperties != null);
                Count(Enum != null);
                Count(Not != null);
                Count(AnyOf != null);
                Count(HasDefaultValue);
                Count(MinLength != null);
                Count(MaxLength != null);

                return count;

                void Count(bool isKeywordSpecified)
                {
                    count += isKeywordSpecified ? 1 : 0;
                }
            }
        }

        public void MakeNullable()
        {
            if (_trueOrFalse != null)
            {
                // boolean schemas do not admit type keywords.
                return;
            }

            if (Type != RdnSchemaType.Any)
            {
                Type |= RdnSchemaType.Null;
            }
        }

        public RdnNode ToRdnNode(RdnSchemaExporterOptions options)
        {
            if (_trueOrFalse is { } boolSchema)
            {
                return CompleteSchema((RdnNode)boolSchema);
            }

            var objSchema = new RdnObject();

            if (Ref != null)
            {
                objSchema.Add(RefPropertyName, Ref);
            }

            if (Comment != null)
            {
                objSchema.Add(CommentPropertyName, Comment);
            }

            if (MapSchemaType(Type) is RdnNode type)
            {
                objSchema.Add(TypePropertyName, type);
            }

            if (Format != null)
            {
                objSchema.Add(FormatPropertyName, Format);
            }

            if (Pattern != null)
            {
                objSchema.Add(PatternPropertyName, Pattern);
            }

            if (Constant != null)
            {
                objSchema.Add(ConstPropertyName, Constant);
            }

            if (Properties != null)
            {
                var properties = new RdnObject();
                foreach (KeyValuePair<string, RdnSchema> property in Properties)
                {
                    properties.Add(property.Key, property.Value.ToRdnNode(options));
                }

                objSchema.Add(PropertiesPropertyName, properties);
            }

            if (Required != null)
            {
                var requiredArray = new RdnArray();
                foreach (string requiredProperty in Required)
                {
                    requiredArray.Add((RdnNode)requiredProperty);
                }

                objSchema.Add(RequiredPropertyName, requiredArray);
            }

            if (Items != null)
            {
                objSchema.Add(ItemsPropertyName, Items.ToRdnNode(options));
            }

            if (AdditionalProperties != null)
            {
                objSchema.Add(AdditionalPropertiesPropertyName, AdditionalProperties.ToRdnNode(options));
            }

            if (Enum != null)
            {
                objSchema.Add(EnumPropertyName, Enum);
            }

            if (Not != null)
            {
                objSchema.Add(NotPropertyName, Not.ToRdnNode(options));
            }

            if (AnyOf != null)
            {
                RdnArray anyOfArray = [];
                foreach (RdnSchema schema in AnyOf)
                {
                    anyOfArray.Add(schema.ToRdnNode(options));
                }

                objSchema.Add(AnyOfPropertyName, anyOfArray);
            }

            if (HasDefaultValue)
            {
                objSchema.Add(DefaultPropertyName, DefaultValue);
            }

            if (MinLength is int minLength)
            {
                objSchema.Add(MinLengthPropertyName, (RdnNode)minLength);
            }

            if (MaxLength is int maxLength)
            {
                objSchema.Add(MaxLengthPropertyName, (RdnNode)maxLength);
            }

            return CompleteSchema(objSchema);

            RdnNode CompleteSchema(RdnNode schema)
            {
                if (ExporterContext is { } context)
                {
                    Debug.Assert(options.TransformSchemaNode != null, "context should only be populated if a callback is present.");
                    // Apply any user-defined transformations to the schema.
                    return options.TransformSchemaNode(context, schema);
                }

                return schema;
            }
        }

        /// <summary>
        /// If the schema is boolean, replaces it with a semantically
        /// equivalent object schema that allows appending keywords.
        /// </summary>
        public static void EnsureMutable(ref RdnSchema schema)
        {
            switch (schema._trueOrFalse)
            {
                case false:
                    schema = new RdnSchema { Not = CreateTrueSchema() };
                    break;
                case true:
                    schema = new RdnSchema();
                    break;
            }
        }

        private static ReadOnlySpan<RdnSchemaType> s_schemaValues =>
        [
            // NB the order of these values influences order of types in the rendered schema
            RdnSchemaType.String,
            RdnSchemaType.Integer,
            RdnSchemaType.Number,
            RdnSchemaType.Boolean,
            RdnSchemaType.Array,
            RdnSchemaType.Object,
            RdnSchemaType.Null,
        ];

        private void VerifyMutable()
        {
            Debug.Assert(_trueOrFalse is null, "Schema is not mutable");
            if (_trueOrFalse is not null)
            {
                Throw();
                static void Throw() => throw new InvalidOperationException();
            }
        }

        public static RdnNode? MapSchemaType(RdnSchemaType schemaType)
        {
            if (schemaType is RdnSchemaType.Any)
            {
                return null;
            }

            if (ToIdentifier(schemaType) is string identifier)
            {
                return identifier;
            }

            var array = new RdnArray();
            foreach (RdnSchemaType type in s_schemaValues)
            {
                if ((schemaType & type) != 0)
                {
                    array.Add((RdnNode)ToIdentifier(type)!);
                }
            }

            return array;

            static string? ToIdentifier(RdnSchemaType schemaType)
            {
                return schemaType switch
                {
                    RdnSchemaType.Null => "null",
                    RdnSchemaType.Boolean => "boolean",
                    RdnSchemaType.Integer => "integer",
                    RdnSchemaType.Number => "number",
                    RdnSchemaType.String => "string",
                    RdnSchemaType.Array => "array",
                    RdnSchemaType.Object => "object",
                    _ => null,
                };
            }
        }
    }
}
