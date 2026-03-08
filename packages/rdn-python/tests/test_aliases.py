import rdn


def test_parse_is_loads():
    assert rdn.parse is rdn.loads


def test_stringify_is_dumps():
    assert rdn.stringify is rdn.dumps


def test_aliases_in_all():
    assert "parse" in rdn.__all__
    assert "stringify" in rdn.__all__


def test_parse_works():
    assert rdn.parse('{"a":1}') == {"a": 1}


def test_stringify_works():
    result = rdn.stringify({"a": 1})
    assert '"a"' in result
