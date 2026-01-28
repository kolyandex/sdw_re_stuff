package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"hash/crc32"
	"io"
	"os"
)

func check(err error) {
	if err != nil {
		panic(err)
	}
}

func main() {
	if len(os.Args) < 2 {
		fmt.Println("specify .WAR path")
		return
	}

	path := os.Args[1]

	war, err := os.OpenFile(path, os.O_RDWR, 0)
	check(err)
	defer war.Close()

	_, err = war.Seek(4, io.SeekStart)
	check(err)

	reader := bufio.NewReader(war)
	crc := crc32.NewIEEE()

	_, err = io.Copy(crc, reader)
	check(err)
	checksum := crc.Sum32()

	// SDW's CRC32 doesn't do the final XOR, while the golang one does
	// let's replicate this the lazy way by just flipping all the bits again :D
	checksum = ^checksum

	_, err = war.Seek(0, io.SeekStart)
	check(err)

	binary.Write(war, binary.LittleEndian, checksum)

	fmt.Printf("wrote new checksum! 0x%x\n", checksum)
}
