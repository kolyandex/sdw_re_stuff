#pragma endian little

struct File {
    u32 jpg_offset;
    u32 jpg_size;
    char filename[16];
    
    u8 jpg[jpg_size] @ jpg_offset;
} [[hex::visualize("image", jpg)]];

struct SDW {
    u64 unused;
    u32 num_files;
    
    File files[num_files];
};

SDW sdw @ 0;