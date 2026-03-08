"""Command-line tool for validating and pretty-printing RDN.

Usage: python -m rdn [infile] [outfile]
"""
from __future__ import annotations

import argparse
import sys

import rdn


def main() -> int:
    parser = argparse.ArgumentParser(
        prog="python -m rdn",
        description="A simple command line interface for rdn module "
                    "to validate and pretty-print RDN documents.",
    )
    parser.add_argument("infile", nargs="?", type=argparse.FileType("r"),
                        default=sys.stdin,
                        help="an RDN file to be validated or pretty-printed")
    parser.add_argument("outfile", nargs="?", type=argparse.FileType("w"),
                        default=sys.stdout,
                        help="write the output of infile to outfile")
    parser.add_argument("--sort-keys", action="store_true", default=False,
                        help="sort the output of dictionaries alphabetically by key")
    parser.add_argument("--no-ensure-ascii", action="store_true", default=False,
                        help="disable escaping of non-ASCII characters")

    group = parser.add_mutually_exclusive_group()
    group.add_argument("--indent", type=int, default=4,
                       help="separate items with newlines and use this number "
                            "of spaces for indentation")
    group.add_argument("--tab", action="store_true", default=False,
                       help="separate items with newlines and use tabs for indentation")
    group.add_argument("--no-indent", action="store_true", default=False,
                       help="separate items with spaces rather than newlines")
    group.add_argument("--compact", action="store_true", default=False,
                       help="suppress all whitespace separation (most compact)")

    args = parser.parse_args()

    # Determine indent and separators
    indent: int | str | None = args.indent
    separators: tuple[str, str] | None = None
    if args.tab:
        indent = "\t"
    elif args.no_indent:
        indent = None
        separators = (", ", ": ")
    elif args.compact:
        indent = None
        separators = (",", ":")

    try:
        text = args.infile.read()
        obj = rdn.loads(text)
        output = rdn.dumps(obj, sort_keys=args.sort_keys, indent=indent,
                           ensure_ascii=not args.no_ensure_ascii,
                           separators=separators)
        args.outfile.write(output)
        args.outfile.write("\n")
        return 0
    except rdn.RDNDecodeError as e:
        print(str(e), file=sys.stderr)
        return 1
    except (KeyboardInterrupt, BrokenPipeError):
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
