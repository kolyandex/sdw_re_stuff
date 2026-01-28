package main

type SamClass struct {
	SECONDCAMERABOX     uint32
	AUTHORIZEDZONE      uint32
	CATCHABLESHEEPBOX   uint32
	CIN01               uint32
	CIN01BOX            uint32
	CIN01FLAGS          uint32
	CIN01SHEEPBOX       uint32
	CIN01TEXT           uint32
	DEATHDURATIONMS     uint32
	DISTCATCH_PATROL    uint32
	DNTTRYTOCATCHSHPBOX uint32
	GREENZONEBOX        uint32
	HEADSPEED           uint32
	HIDINGBOXES         uint32
	HYPNOTIZEDZONE      uint32
	MAXANGLE            uint32
	MODE                uint32
	ORANGEZONEBOX       uint32
	PUTSHEEPBOX         uint32
	TRAJECTORY          uint32
	WATCHROBOTTIMEMS    uint32
	WOLFCANBEHITZONE    uint32
}

type CheckpointManagerCheck struct {
	BOXES       uint32
	ORIENTATION uint32
	SHEEP       uint32
	WOLF        uint32
}

type CheckpointManagerClass struct {
	Checks           [8]CheckpointManagerCheck
	SHEEPANDWOLFONLY uint32
}
