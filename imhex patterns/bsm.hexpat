#pragma endian little

import type.magic;

enum Language : u8 {
    en = 0x1,
    fr = 0x2,
    de = 0x4,
    nl = 0x8,
    es = 0x10,
    it = 0x20,
    pt = 0x40,
};

struct String {
    Language lang_id;
    u16 string_id;
    u8 section_id;
    u32 size;
    char string[size];
};

struct BSM {
    type::Magic<"GREETINGV1.0"> magic;
    u32 size;
    String strings[size];
};

BSM bsm @ 0;