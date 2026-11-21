# MorphCdr: CorelDRAW reader

Reads CorelDRAW drawings (.cdr, .cmx) and writes each page as an SVG, so Morph converts them without LibreOffice.
Morph runs it as a separate process with a time limit (src/Filee.Engines/Vector/CorelDrawConverter.cs).

- `MorphCdr.cpp` and `icu-shim/`: from OrbitDrop's `OrbitDrop.CdrReader` (MIT, https://github.com/i3sam/OrbitDrop),
  made portable (Windows, Linux, macOS).
- Built on Windows by `build/build-cdr-reader.ps1` (MSVC) from unmodified upstream sources, checked by SHA-256:
  libcdr 0.1.9 and librevenge 0.0.6 (MPL 2.0), lcms2 2.16 (MIT), zlib 1.3.1 (zlib), Boost 1.86 headers (BSL 1.0).
  Their licences go into `engines\cdr\licenses` beside the exe.
- On Linux it builds against the system's libcdr: `g++ -O2 -std=c++17 MorphCdr.cpp $(pkg-config --cflags --libs
  libcdr-0.1 librevenge-0.0 librevenge-generators-0.0 librevenge-stream-0.0) -o engines/cdr/MorphCdr`.
