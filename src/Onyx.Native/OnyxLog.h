#pragma once

// Lightweight file logger for diagnosing the media source *inside* the Windows
// Frame Server process (where a debugger/console is not available).
// Writes to C:\ProgramData\Onyx\onyx.log. No-op-safe if the file can't be opened.

namespace onyx {
void Log(const char* fmt, ...);
}  // namespace onyx
