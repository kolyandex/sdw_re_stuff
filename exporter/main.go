package main

import (
	"flag"
	"fmt"
	"os"
)

func check(err error) {
	if err != nil {
		panic(err)
	}
}

func main() {
	if len(os.Args) < 2 {
		fmt.Println("commands: dav, davpages, snd, sndpack, bsm, bsmpack, modelexport, lvlexport, modelfolder, skyboxexport")
		fmt.Println("type a command and \"-h\" for usage info")
		return
	}

	davCmd := flag.NewFlagSet("dav", flag.ExitOnError)
	davPath := davCmd.String("p", "", "DAV path")
	davOutPath := davCmd.String("o", "out", "Output directory")

	davPagesCmd := flag.NewFlagSet("davpages", flag.ExitOnError)
	davPagesPath := davPagesCmd.String("p", "", "DAV path")
	davPagesOutPath := davPagesCmd.String("o", "out", "Output directory")

	sndCmd := flag.NewFlagSet("snd", flag.ExitOnError)
	sndPath := sndCmd.String("p", "", "SND path")
	sndOutPath := sndCmd.String("o", "out", "Output directory")

	sndPackCmd := flag.NewFlagSet("sndpack", flag.ExitOnError)
	sndPackDirPath := sndPackCmd.String("p", "", "WAV directory path")
	sndPackOutPath := sndPackCmd.String("o", "out.SND", "Output SND path")

	bsmCmd := flag.NewFlagSet("bsm", flag.ExitOnError)
	bsmPath := bsmCmd.String("p", "", "BSM path")
	bsmOutPath := bsmCmd.String("o", "out.json", "Output JSON path")

	bsmPackCmd := flag.NewFlagSet("bsmpack", flag.ExitOnError)
	bsmPackPath := bsmPackCmd.String("p", "", "JSON path")
	bsmPackOutPath := bsmPackCmd.String("o", "out.BSM", "Output BSM path")

	modelExportCmd := flag.NewFlagSet("modelexport", flag.ExitOnError)
	modelExportPath := modelExportCmd.String("p", "", "WAR path")
	modelExportDavPath := modelExportCmd.String("d", "", "DAV path")
	modelExportOutPath := modelExportCmd.String("o", "model.glb", "Output GLB path")
	modelExportIdx := modelExportCmd.Uint("i", 0, "Model resource index")

	levelExportCmd := flag.NewFlagSet("lvlexport", flag.ExitOnError)
	levelExportPath := levelExportCmd.String("p", "", "WAR path")
	levelExportDavPath := levelExportCmd.String("d", "", "DAV path")
	levelExportOutPath := levelExportCmd.String("o", "level.glb", "Output GLB path")

	modelFolderCmd := flag.NewFlagSet("modelfolder", flag.ExitOnError)
	modelFolderPath := modelFolderCmd.String("p", "", "WAR path")
	modelFolderDavPath := modelFolderCmd.String("d", "", "DAV path")
	modelFolderOutPath := modelFolderCmd.String("o", "level", "Output directory")

	skyboxExportCmd := flag.NewFlagSet("skyboxexport", flag.ExitOnError)
	skyboxExportPath := skyboxExportCmd.String("p", "", "WAR path")
	skyboxExportDavPath := skyboxExportCmd.String("d", "", "DAV path")
	skyboxExportOutPath := skyboxExportCmd.String("o", "skybox.glb", "Output GLB path")

	if len(os.Args) < 2 {
		fmt.Println("no subcommand specified")
		os.Exit(1)
	}

	switch os.Args[1] {
	case "dav":
		check(davCmd.Parse(os.Args[2:]))
		if len(*davPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		davExtract(*davPath, *davOutPath)
	case "davpages":
		check(davPagesCmd.Parse(os.Args[2:]))
		if len(*davPagesPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		davExtractPages(*davPagesPath, *davPagesOutPath)
	case "snd":
		check(sndCmd.Parse(os.Args[2:]))
		if len(*sndPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		sndExtract(*sndPath, *sndOutPath)
	case "sndpack":
		check(sndPackCmd.Parse(os.Args[2:]))
		if len(*sndPackDirPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		sndPack(*sndPackDirPath, *sndPackOutPath)
	case "bsm":
		check(bsmCmd.Parse(os.Args[2:]))
		if len(*bsmPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		bsmToJson(*bsmPath, *bsmOutPath)
	case "bsmpack":
		check(bsmPackCmd.Parse(os.Args[2:]))
		if len(*bsmPackPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		jsonToBsm(*bsmPackPath, *bsmPackOutPath)
	case "modelexport":
		check(modelExportCmd.Parse(os.Args[2:]))
		if len(*modelExportPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		modelExport(*modelExportPath, *modelExportDavPath, *modelExportOutPath, uint32(*modelExportIdx))
	case "lvlexport":
		check(levelExportCmd.Parse(os.Args[2:]))
		if len(*levelExportPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		levelExport(*levelExportPath, *levelExportDavPath, *levelExportOutPath)
	case "modelfolder":
		check(modelFolderCmd.Parse(os.Args[2:]))
		if len(*modelFolderPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		modelExportToFolder(*modelFolderPath, *modelFolderDavPath, *modelFolderOutPath)
	case "skyboxexport":
		check(skyboxExportCmd.Parse(os.Args[2:]))
		if len(*skyboxExportPath) == 0 {
			fmt.Println("no path specified")
			os.Exit(1)
		}
		skyboxExport(*skyboxExportPath, *skyboxExportDavPath, *skyboxExportOutPath)

	default:
		fmt.Println("invalid subcommand specified")
		os.Exit(1)
	}
}
