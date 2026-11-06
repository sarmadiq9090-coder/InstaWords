"""Independent check of InstaWords.xlam with oletools (olefile + olevba).

    python -I tests/vba/olevba_check.py out/InstaWords.xlam vba/Tafqit.bas

Opens xl/vbaProject.bin with olefile in strict mode (any incorrect structure raises), lets olevba
find and decompress every module, and compares the TafqitLib module with the .bas byte for byte.
Exit status 0 and "olevba: OK" when everything matches.
"""
import io
import sys
import zipfile

import olefile
from oletools import olevba


def main(xlam_path, bas_path):
    bas = open(bas_path, "rb").read()
    with zipfile.ZipFile(xlam_path) as z:
        if z.testzip() is not None:
            sys.exit("zip: bad CRC")
        bin_data = z.read("xl/vbaProject.bin")

    ole = olefile.OleFileIO(io.BytesIO(bin_data), raise_defects=olefile.DEFECT_INCORRECT)
    streams = sorted("/".join(p) for p in ole.listdir())
    expected = ["PROJECT", "PROJECTwm", "VBA/Sheet1", "VBA/TafqitLib", "VBA/ThisWorkbook", "VBA/_VBA_PROJECT", "VBA/dir"]
    if streams != expected:
        sys.exit("olefile: streams %r" % streams)

    parser = olevba.VBA_Parser(xlam_path)
    if not parser.detect_vba_macros():
        sys.exit("olevba: no VBA found")
    modules = {}
    for _, stream_path, vba_filename, code in parser.extract_all_macros():
        modules[vba_filename] = (stream_path, code)
    if sorted(modules) != ["Sheet1.cls", "TafqitLib.bas", "ThisWorkbook.cls"]:
        sys.exit("olevba: modules %r" % sorted(modules))
    stream_path, code = modules["TafqitLib.bas"]
    if stream_path != "VBA/TafqitLib":
        sys.exit("olevba: TafqitLib in %s" % stream_path)
    got = code.encode("cp1252") if isinstance(code, str) else code
    if got != bas:
        n = next((i for i, (a, b) in enumerate(zip(got, bas)) if a != b), min(len(got), len(bas)))
        sys.exit("olevba: TafqitLib differs from %s at byte %d (%d vs %d bytes)" % (bas_path, n, len(got), len(bas)))
    # The raw stream as well, through olevba's own MS-OVBA decompressor.
    raw = olevba.decompress_stream(bytearray(ole.openstream("VBA/TafqitLib").read()))
    if raw != bas:
        sys.exit("olevba: decompress_stream differs")
    parser.close()
    ole.close()
    print("olevba: OK (%s, oletools %s): %d modules, TafqitLib = %s (%d bytes)" % (xlam_path, olevba.__version__, len(modules), bas_path, len(bas)))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
