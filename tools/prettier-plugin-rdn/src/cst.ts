/** Base for all CST nodes — every node carries source positions */
export interface CstNodeBase {
  type: string;
  start: number;
  end: number;
}

export interface DocumentNode extends CstNodeBase {
  type: "Document";
  body: RdnCstNode;
}

export interface StringLiteralNode extends CstNodeBase {
  type: "StringLiteral";
  value: string;
  raw: string;
}

export interface NumberLiteralNode extends CstNodeBase {
  type: "NumberLiteral";
  raw: string;
}

export interface BigIntLiteralNode extends CstNodeBase {
  type: "BigIntLiteral";
  raw: string;
}

export interface BooleanLiteralNode extends CstNodeBase {
  type: "BooleanLiteral";
  value: boolean;
}

export interface NullLiteralNode extends CstNodeBase {
  type: "NullLiteral";
}

export interface NaNLiteralNode extends CstNodeBase {
  type: "NaNLiteral";
}

export interface InfinityLiteralNode extends CstNodeBase {
  type: "InfinityLiteral";
  negative: boolean;
}

export interface DateTimeLiteralNode extends CstNodeBase {
  type: "DateTimeLiteral";
  raw: string;
}

export interface TimeOnlyLiteralNode extends CstNodeBase {
  type: "TimeOnlyLiteral";
  raw: string;
}

export interface DurationLiteralNode extends CstNodeBase {
  type: "DurationLiteral";
  raw: string;
}

export interface BinaryLiteralNode extends CstNodeBase {
  type: "BinaryLiteral";
  encoding: "base64" | "hex";
  raw: string;
}

export interface RegExpLiteralNode extends CstNodeBase {
  type: "RegExpLiteral";
  raw: string;
}

export interface ArrayNode extends CstNodeBase {
  type: "Array";
  elements: RdnCstNode[];
}

export interface TupleNode extends CstNodeBase {
  type: "Tuple";
  elements: RdnCstNode[];
}

export interface ObjectPropertyNode extends CstNodeBase {
  type: "ObjectProperty";
  key: StringLiteralNode;
  value: RdnCstNode;
}

export interface ObjectNode extends CstNodeBase {
  type: "Object";
  properties: ObjectPropertyNode[];
}

export interface MapEntryNode extends CstNodeBase {
  type: "MapEntry";
  key: RdnCstNode;
  value: RdnCstNode;
}

export interface MapNode extends CstNodeBase {
  type: "Map";
  entries: MapEntryNode[];
  explicit: boolean;
}

export interface SetNode extends CstNodeBase {
  type: "Set";
  elements: RdnCstNode[];
  explicit: boolean;
}

/** Union of all value-level CST nodes */
export type RdnCstNode =
  | StringLiteralNode
  | NumberLiteralNode
  | BigIntLiteralNode
  | BooleanLiteralNode
  | NullLiteralNode
  | NaNLiteralNode
  | InfinityLiteralNode
  | DateTimeLiteralNode
  | TimeOnlyLiteralNode
  | DurationLiteralNode
  | BinaryLiteralNode
  | RegExpLiteralNode
  | ArrayNode
  | TupleNode
  | ObjectNode
  | MapNode
  | SetNode;
