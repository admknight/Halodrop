// MorphCdr: reads CorelDRAW drawings (.cdr, and Corel's .cmx) and writes each page as an SVG, so Morph converts them
// without LibreOffice. Based on OrbitDrop's OrbitDropCdr (MIT, github.com/i3sam/OrbitDrop); made portable for Morph
// (Windows, Linux, macOS).
//
//   MorphCdr <drawing> <output folder>
//
// Writes page-1.svg, page-2.svg ... and prints how many. Exit codes: 0 done; 1 bad arguments or a file problem;
// 2 not a CorelDRAW drawing it can read (or encrypted); 3 the drawing couldn't be read.
//
// Built on libcdr and librevenge (MPL 2.0, The Document Foundation: the reader Inkscape and LibreOffice use), with
// lcms2 (MIT), zlib (zlib licence) and ICU (Windows' own on Windows). See build/build-cdr-reader.ps1.
//
// Morph runs it as a separate process with a time limit, so a damaged or hostile file can only fail its own conversion.

#include <cstdio>
#include <cstring>
#include <algorithm>
#include <string>
#include <vector>
#include <librevenge/librevenge.h>
#include <librevenge-stream/librevenge-stream.h>
#include <libcdr/libcdr.h>

#ifdef _WIN32
#include <windows.h>
typedef std::wstring PathString;
#define PATH_SEPARATOR L"\\"
#define PAGE_PREFIX L"page-"
#define PAGE_SUFFIX L".svg"
static std::wstring ToPathString(unsigned i) { return std::to_wstring(i); }
#else
typedef std::string PathString;
#define PATH_SEPARATOR "/"
#define PAGE_PREFIX "page-"
#define PAGE_SUFFIX ".svg"
static std::string ToPathString(unsigned i) { return std::to_string(i); }
#endif

static const unsigned long long MaxInput = 512ull * 1024 * 1024;

// The whole file in memory: works with any file name (librevenge's own file stream takes narrow paths only).
static bool ReadAll(const PathString& path, std::vector<unsigned char>& data)
{
#ifdef _WIN32
    FILE* f = _wfopen(path.c_str(), L"rb");
#else
    FILE* f = fopen(path.c_str(), "rb");
#endif
    if (!f) return false;
    bool ok = fseek(f, 0, SEEK_END) == 0;
    long long size = ok ? static_cast<long long>(ftell(f)) : -1;
    ok = ok && size > 0 && static_cast<unsigned long long>(size) <= MaxInput && fseek(f, 0, SEEK_SET) == 0;
    if (ok)
    {
        data.resize(static_cast<size_t>(size));
        ok = fread(data.data(), 1, data.size(), f) == data.size();
    }
    fclose(f);
    return ok;
}

static bool WriteAll(const PathString& path, const char* text, size_t length)
{
#ifdef _WIN32
    FILE* f = _wfopen(path.c_str(), L"wb");
#else
    FILE* f = fopen(path.c_str(), "wb");
#endif
    if (!f) return false;
    static const char header[] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
    bool ok = fwrite(header, 1, sizeof(header) - 1, f) == sizeof(header) - 1 && fwrite(text, 1, length, f) == length;
    return fclose(f) == 0 && ok;
}

static int Run(const PathString& drawing, const PathString& folder)
{
    std::vector<unsigned char> data;
    if (!ReadAll(drawing, data)) { fputs("Can't read the file.\n", stderr); return 1; }

    librevenge::RVNGStringStream input(data.data(), static_cast<unsigned>(data.size()));
    librevenge::RVNGStringVector pages;
    librevenge::RVNGSVGDrawingGenerator generator(pages, ""); // plain <svg>, no namespace prefix
    bool parsed = false;
    try
    {
        if (libcdr::CDRDocument::isSupported(&input)) parsed = libcdr::CDRDocument::parse(&input, &generator);
        else if (libcdr::CMXDocument::isSupported(&input)) parsed = libcdr::CMXDocument::parse(&input, &generator);
        else { fputs("Not a CorelDRAW drawing this version can read, or it's encrypted.\n", stderr); return 2; }
    }
    catch (...) { parsed = false; }
    if (!parsed || pages.empty()) { fputs("The drawing couldn't be read.\n", stderr); return 3; }

    for (unsigned i = 0; i < pages.size(); i++)
    {
        PathString out = folder + PATH_SEPARATOR + PAGE_PREFIX + ToPathString(i + 1) + PAGE_SUFFIX;
        if (!WriteAll(out, pages[i].cstr(), pages[i].size())) { fputs("Can't write the result.\n", stderr); return 1; }
    }
    printf("%u\n", pages.size());
    return 0;
}

#ifdef _WIN32
int wmain(int argc, wchar_t** argv)
{
    // Never show Windows' crash or "insert disk" dialogs: a failure is just an exit code.
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    if (argc != 3) { fputs("Usage: MorphCdr <drawing> <output folder>\n", stderr); return 1; }
    return Run(argv[1], argv[2]);
}
#else
int main(int argc, char** argv)
{
    if (argc != 3) { fputs("Usage: MorphCdr <drawing> <output folder>\n", stderr); return 1; }
    return Run(argv[1], argv[2]);
}
#endif
