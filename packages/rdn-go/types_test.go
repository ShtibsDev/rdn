package rdn

import "testing"

func TestTimeOnlyString(t *testing.T) {
	tests := []struct {
		t    TimeOnly
		want string
	}{
		{TimeOnly{14, 30, 0, 0}, "14:30:00"},
		{TimeOnly{23, 59, 59, 999}, "23:59:59.999"},
		{TimeOnly{0, 0, 0, 0}, "00:00:00"},
		{TimeOnly{9, 5, 3, 1}, "09:05:03.001"},
	}
	for _, tt := range tests {
		if got := tt.t.String(); got != tt.want {
			t.Errorf("TimeOnly.String() = %q, want %q", got, tt.want)
		}
	}
}

func TestDurationString(t *testing.T) {
	d := Duration{ISO: "P1Y2M3DT4H5M6S"}
	if d.String() != "P1Y2M3DT4H5M6S" {
		t.Errorf("got %q", d.String())
	}
}

func TestRegExpString(t *testing.T) {
	r := RegExp{Source: "test", Flags: "gi"}
	if r.String() != "/test/gi" {
		t.Errorf("got %q", r.String())
	}
}

func TestNumberType(t *testing.T) {
	n := Number("42")
	f, err := n.Float64()
	if err != nil || f != 42 {
		t.Errorf("Float64() = %v, %v", f, err)
	}
	i, err := n.Int64()
	if err != nil || i != 42 {
		t.Errorf("Int64() = %v, %v", i, err)
	}
	bi, err := n.BigInt()
	if err != nil || bi.Int64() != 42 {
		t.Errorf("BigInt() = %v, %v", bi, err)
	}
	if n.String() != "42" {
		t.Errorf("String() = %q", n.String())
	}
}
