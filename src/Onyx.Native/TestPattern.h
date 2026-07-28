#pragma once
#include <cstdint>

// Onyx test-pattern generator (milestone 1a).
//
// Pure, dependency-free frame generator used to validate the virtual-camera
// pipeline before any real webcam / ML is involved. Produces a 32-bit BGRA
// (RGB32) top-down frame: SMPTE-style vertical colour bars plus a white bar
// that sweeps horizontally each frame so motion is visible in the consumer app.

namespace onyx {

// Writes one BGRA frame (4 bytes/pixel, top-down) into `dst`.
// `dst` must hold at least width * height * 4 bytes.
// `frameIndex` advances the moving sweep bar (any monotonically increasing int).
void GenerateTestPattern(uint8_t* dst, int width, int height, uint64_t frameIndex);

}  // namespace onyx
