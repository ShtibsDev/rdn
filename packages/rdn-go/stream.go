package rdn

import "io"

// Decoder reads and decodes a single RDN value from an input stream.
type Decoder struct {
	r io.Reader
}

// NewDecoder returns a new Decoder that reads from r.
func NewDecoder(r io.Reader) *Decoder {
	return &Decoder{r: r}
}

// Decode reads the entire input stream and parses it as a single RDN value.
func (dec *Decoder) Decode(v *Value) error {
	data, err := io.ReadAll(dec.r)
	if err != nil {
		return err
	}
	val, err := Parse(data)
	if err != nil {
		return err
	}
	*v = val
	return nil
}

// Encoder writes RDN values to an output stream.
type Encoder struct {
	w      io.Writer
	indent string
	prefix string
}

// NewEncoder returns a new Encoder that writes to w.
func NewEncoder(w io.Writer) *Encoder {
	return &Encoder{w: w}
}

// SetIndent instructs the encoder to format each subsequent encoded value
// with indentation, mirroring StringifyIndent's prefix and indent parameters.
func (enc *Encoder) SetIndent(prefix, indent string) {
	enc.prefix = prefix
	enc.indent = indent
}

// Encode writes the RDN encoding of v to the stream, followed by a newline character.
func (enc *Encoder) Encode(v Value) error {
	e := newEncoder(enc.indent, enc.prefix)
	defer putEncodeState(e.buf)

	if err := e.encode(v); err != nil {
		return err
	}
	e.buf.WriteByte('\n')

	_, err := enc.w.Write(e.buf.Bytes())
	return err
}

// EncodeValue marshals v into an rdn.Value and writes it to the stream.
func (enc *Encoder) EncodeValue(v any) error {
	val, err := MarshalValue(v)
	if err != nil {
		return err
	}
	return enc.Encode(val)
}

// DecodeValue reads a Value from the stream and unmarshals it into v.
func (dec *Decoder) DecodeValue(v any) error {
	var val Value
	if err := dec.Decode(&val); err != nil {
		return err
	}
	return UnmarshalValue(val, v)
}
