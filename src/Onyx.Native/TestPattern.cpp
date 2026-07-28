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

}  // namespace onyx
