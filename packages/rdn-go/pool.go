package rdn

import (
	"bytes"
	"sync"
)

// encodeState wraps a bytes.Buffer with pooling support.
type encodeState struct {
	bytes.Buffer
}

var encodeStatePool = sync.Pool{
	New: func() any { return new(encodeState) },
}

func getEncodeState() *encodeState {
	e := encodeStatePool.Get().(*encodeState)
	e.Reset()
	return e
}

func putEncodeState(e *encodeState) {
	// Don't pool very large buffers (> 64 KB)
	if e.Cap() > 64*1024 {
		return
	}
	encodeStatePool.Put(e)
}
