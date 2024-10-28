#include <iostream>
#include <windows.h>
#include <thread>
#include <condition_variable>

struct event {
    std::chrono::time_point<std::chrono::system_clock> time;
    unsigned char vkCode;
    bool release;
};

unsigned char keyMapping(const unsigned char code) {
    switch(code) {
        case 27: return 0;    // Escape
        case 112: return 1;    // F1
        case 113: return 2;    // F2
        case 114: return 3;    // F3
        case 115: return 4;    // F4
        case 116: return 5;    // F5
        case 117: return 6;    // F6
        case 118: return 7;    // F7
        case 119: return 8;    // F8
        case 120: return 9;    // F9
        case 121: return 10;   // F10
        case 122: return 11;   // F11
        case 123: return 12;   // F12
        case 124: return 13;   // F13
        case 125: return 14;   // F14
        case 126: return 15;   // F15
        case 127: return 16;   // F16
        case 128: return 17;   // F17
        case 129: return 18;   // F18
        case 130: return 19;   // F19
        case 131: return 20;   // F20
        case 132: return 21;   // F21
        case 133: return 22;   // F22
        case 134: return 23;   // F23
        case 135: return 24;   // F24
        case 192: return 25;   // Grave
        case 49: return 26;    // Alpha1
        case 50: return 27;    // Alpha2
        case 51: return 28;    // Alpha3
        case 52: return 29;    // Alpha4
        case 53: return 30;    // Alpha5
        case 54: return 31;    // Alpha6
        case 55: return 32;    // Alpha7
        case 56: return 33;    // Alpha8
        case 57: return 34;    // Alpha9
        case 48: return 35;    // Alpha0
        case 189: return 36;   // Minus
        case 187: return 37;   // Equal
        case 8: return 38;     // Backspace
        case 9: return 39;     // Tab
        case 81: return 40;    // Q
        case 87: return 41;    // W
        case 69: return 42;    // E
        case 82: return 43;    // R
        case 84: return 44;    // T
        case 89: return 45;    // Y
        case 85: return 46;    // U
        case 73: return 47;    // I
        case 79: return 48;    // O
        case 80: return 49;    // P
        case 219: return 50;   // LeftBrace
        case 221: return 51;   // RightBrace
        case 220: return 52;   // BackSlash
        case 20: return 53;    // CapsLock
        case 65: return 54;    // A
        case 83: return 55;    // S
        case 68: return 56;    // D
        case 70: return 57;    // F
        case 71: return 58;    // G
        case 72: return 59;    // H
        case 74: return 60;    // J
        case 75: return 61;    // K
        case 76: return 62;    // L
        case 186: return 63;   // Semicolon
        case 222: return 64;   // Apostrophe
        case 13: return 65;    // Enter
        case 160: return 66;   // LShift
        case 90: return 67;    // Z
        case 88: return 68;    // X
        case 67: return 69;    // C
        case 86: return 70;    // V
        case 66: return 71;    // B
        case 78: return 72;    // N
        case 77: return 73;    // M
        case 188: return 74;   // Comma
        case 190: return 75;   // Dot
        case 191: return 76;   // Slash
        case 161: return 77;   // RShift
        case 162: return 78;   // LControl
        case 91: return 79;    // Super
        case 164: return 80;   // LAlt
        case 32: return 81;    // Space
        case 165: return 82;   // RAlt
        case 163: return 83;   // RControl
        case 44: return 84;    // PrintScreen
        case 145: return 85;   // ScrollLock
        case 19: return 86;    // PauseBreak
        case 45: return 87;    // Insert
        case 36: return 88;    // Home
        case 33: return 89;    // PageUp
        case 46: return 90;    // Delete
        case 35: return 91;    // End
        case 34: return 92;    // PageDown
        case 38: return 93;    // ArrowUp
        case 37: return 94;    // ArrowLeft
        case 40: return 95;    // ArrowDown
        case 39: return 96;    // ArrowRight
        case 144: return 97;   // NumLock
        case 111: return 98;   // KeypadSlash
        case 106: return 99;   // KeypadAsterisk
        case 109: return 100;  // KeypadMinus
        case 97: return 101;   // Keypad1
        case 98: return 102;   // Keypad2
        case 99: return 103;   // Keypad3
        case 100: return 104;  // Keypad4
        case 101: return 105;  // Keypad5
        case 102: return 106;  // Keypad6
        case 103: return 107;  // Keypad7
        case 104: return 108;  // Keypad8
        case 105: return 109;  // Keypad9
        case 96: return 110;   // Keypad0
        case 110: return 111;  // KeypadDot
        case 107: return 112;  // KeypadPlus
        case 0: return 114;    // MouseLeft
        case 1: return 115;    // MouseRight
        case 2: return 116;    // MouseMiddle
        case 3: return 117;    // MouseX1
        case 4: return 118;    // MouseX2
        default: return 119;    // Unknown
    }
}

event ev[32];
int cur = 0;
int cur2 = 0;
bool keys[256];
bool active = false;
int offset = 0;

std::mutex m;
std::condition_variable cv;
std::condition_variable cv2;

LRESULT CALLBACK callback(int nCode, WPARAM wParam, LPARAM lParam) {
    try {
        if(nCode >= 0) {
            KBDLLHOOKSTRUCT* pKeyboard = (KBDLLHOOKSTRUCT*) lParam;
            int key = pKeyboard->vkCode;
            if(key >= 0 && key < 256) {
                bool down = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
                if(!(keys[key] && down)) {
                    ev[cur] = {std::chrono::system_clock::now(), (byte) key, !down};
                    cur = (cur + 1) % 32;
                    cv.notify_one();
                    keys[key] = down;
                }
            }
        }
    } catch (const std::exception& e) {
        std::cerr << "Error in Callback\n" << e.what() << std::endl;
        exit(-1);
    }
    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

void listener() {
    try {
        while(true) {
            if(!active) {
                std::unique_lock lock(m);
                cv2.wait(lock);
            }
            HHOOK hook = SetWindowsHookEx(WH_KEYBOARD_LL, callback, NULL, 0);
            MSG msg;
            while(GetMessage(&msg, NULL, 0, 0) && active) {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
            UnhookWindowsHookEx(hook);
        }
    } catch (const std::exception& e) {
        std::cerr << "Error in Listener\n" << e.what() << std::endl;
        exit(-1);
    }
}

void writer() {
    try {
        while(true) {
            if(cur == cur2) {
                std::unique_lock lock(m);
                cv.wait(lock);
            }
            event e = ev[cur2];
            long long time = std::chrono::duration_cast<std::chrono::nanoseconds>(e.time.time_since_epoch()).count();
            unsigned char key = keyMapping(e.vkCode);
            std::cout.write((char*) &offset, 1);
            std::cout.write((char*) &time, 8);
            std::cout.write((char*) &e.release, 1);
            std::cout.write((char*) &key, 1);
            std::cout.write((char*) &e.vkCode, 1);
            std::cout.flush();
            if(std::cout.bad()) throw std::runtime_error("Error writing to stdout");
            cur2 = (cur2 + 1) % 32;
            offset = (offset + 1) % 256;
        }
    } catch (const std::exception& e) {
        std::cerr << "Error in Writer\n" << e.what() << std::endl;
        exit(-1);
    }
}

int main() {
    try {
        std::thread t1 = std::thread(listener);
        std::thread t2 = std::thread(writer);
        while(true) {
            byte b;
            std::cin >> b;
            if(!std::cin.good()) return -1;
            switch (b) {
                case 0:
                    offset = 0;
                    active = true;
                    cv2.notify_one();
                    break;
                case 1:
                    active = false;
                    break;
                default:
                    throw std::runtime_error("Invalid command");
            }
        }
    } catch (const std::exception& e) {
        std::cerr << "Error in Reader\n" << e.what() << std::endl;
        return -1;
    }
}
