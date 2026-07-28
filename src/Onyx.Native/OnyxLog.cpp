#include "OnyxLog.h"

#include <windows.h>
#include <cstdio>
#include <cstdarg>

namespace onyx {

void Log(const char* fmt, ...)
{
    char msg[512];
    va_list args;
    va_start(args, fmt);
    int n = vsnprintf(msg, sizeof(msg), fmt, args);
    va_end(args);
    if (n < 0) { return; }

    // Prefix: time + process/thread id.
    SYSTEMTIME st;
    GetLocalTime(&st);
    char line[640];
    int m = _snprintf_s(line, sizeof(line), _TRUNCATE,
        "%02d:%02d:%02d.%03d [pid=%lu tid=%lu] %s\r\n",
        st.wHour, st.wMinute, st.wSecond, st.wMilliseconds,
        GetCurrentProcessId(), GetCurrentThreadId(), msg);
    if (m < 0) { return; }

    HANDLE h = CreateFileA("C:\\ProgramData\\Onyx\\onyx.log",
                           FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) { return; }
    DWORD written = 0;
    WriteFile(h, line, static_cast<DWORD>(m), &written, nullptr);
    CloseHandle(h);
}

}  // namespace onyx
