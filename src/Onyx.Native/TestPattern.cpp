#include "TestPattern.h"

namespace onyx {

namespace {

struct Bgra { uint8_t b, g, r, a; };

inline void PutPixel(uint8_t* dst, int i, Bgra c)
{
    dst[i + 0] = c.b;
    dst[i + 1] = c.g;
    dst[i + 2] = c.r;
    dst[i + 3] = c.a;
}

// Classic SMPTE-style 8-bar palette (left to right).
constexpr Bgra kBars[8] = {
    {192, 192, 192, 255},  // grey
    {  0, 192, 192, 255},  // yellow
    {192, 192,   0, 255},  // cyan
    {  0, 192,   0, 255},  // green
    {192,   0, 192, 255},  // magenta
    {  0,   0, 192, 255},  // red
    {192,   0,   0, 255},  // blue
    {  0,   0,   0, 255},  // black
};

}  // namespace

void GenerateTestPattern(uint8_t* dst, int width, int height, uint64_t frameIndex)
{
    if (!dst || width <= 0 || height <= 0) { return; }

    const int barWidth = width / 8 > 0 ? width / 8 : 1;

    // Moving vertical sweep bar: position advances with frameIndex and wraps.
    const int sweepWidth = width / 40 > 0 ? width / 40 : 2;
    const int sweepX = static_cast<int>((frameIndex * 6ULL) % static_cast<uint64_t>(width));

    for (int y = 0; y < height; ++y)
    {
        uint8_t* row = dst + static_cast<size_t>(y) * width * 4;
        for (int x = 0; x < width; ++x)
        {
            const int barIndex = x / barWidth;
            Bgra c = kBars[barIndex < 8 ? barIndex : 7];

            // Overlay the white sweep bar.
            if (x >= sweepX && x < sweepX + sweepWidth)
            {
                c = {255, 255, 255, 255};
            }

            PutPixel(row, x * 4, c);
        }
    }
}

namespace {

inline uint8_t Clamp8(int v) { return static_cast<uint8_t>(v < 0 ? 0 : (v > 255 ? 255 : v)); }

// Resolve the RGB of the test pattern at (x,y) for the given sweep position.
inline void PatternRgb(int x, int width, int barWidth, int sweepX, int sweepWidth,
                       int& r, int& g, int& b)
{
    if (x >= sweepX && x < sweepX + sweepWidth) { r = g = b = 255; return; }
    int bi = x / barWidth;
    Bgra c = kBars[bi < 8 ? bi : 7];
    r = c.r; g = c.g; b = c.b;
}

}  // namespace

void GenerateTestPatternNV12(uint8_t* dst, int width, int height, uint64_t frameIndex)
{
    if (!dst || width <= 0 || height <= 0) { return; }

    uint8_t* yPlane = dst;
    uint8_t* uvPlane = dst + static_cast<size_t>(width) * height;

    const int barWidth = width / 8 > 0 ? width / 8 : 1;
    const int sweepWidth = width / 40 > 0 ? width / 40 : 2;
    const int sweepX = static_cast<int>((frameIndex * 6ULL) % static_cast<uint64_t>(width));

    // Y plane (BT.601 full-pattern luma).
    for (int y = 0; y < height; ++y)
    {
        for (int x = 0; x < width; ++x)
        {
            int r, g, b;
            PatternRgb(x, width, barWidth, sweepX, sweepWidth, r, g, b);
            int Y = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
            yPlane[static_cast<size_t>(y) * width + x] = Clamp8(Y);
        }
    }

    // UV plane (2x2 subsampled, interleaved U,V).
    for (int y = 0; y < height; y += 2)
    {
        uint8_t* uvRow = uvPlane + static_cast<size_t>(y / 2) * width;
        for (int x = 0; x < width; x += 2)
        {
            int r, g, b;
            PatternRgb(x, width, barWidth, sweepX, sweepWidth, r, g, b);
            int U = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
            int V = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;
            uvRow[x] = Clamp8(U);
            uvRow[x + 1] = Clamp8(V);
        }
    }
}

}  // namespace onyx
