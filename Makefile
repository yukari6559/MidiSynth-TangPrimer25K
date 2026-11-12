# Tang Primer 25K 向けのビットストリームを作る。手順は SHDL-86 で LED の点滅まで確かめたものと同じ。
# yowasp-yosys は今いるディレクトリの下しか読めないので、build/ に入ってから合成する。

TOP        := TangPrimer25KTop
BUILD      := build
CST        := constraints/tangprimer25k.cst
DEVICE     := GW5A-LV25MG121NES
FAMILY     := GW5A-25A

TOOLCHAIN  ?= $(HOME)/Projects/tang25k-blink
YOSYS      ?= yowasp-yosys
NEXTPNR    ?= $(TOOLCHAIN)/tools/build/nextpnr/nextpnr-himbaechel
GOWIN_PACK ?= $(TOOLCHAIN)/.venv/bin/gowin_pack
LOADER     ?= openFPGALoader

SOURCES    := $(wildcard src/MidiSynth/*.cs src/MidiSynth.Emit/*.cs)

.PHONY: verilog synth pnr bitstream load clean

verilog: $(BUILD)/$(TOP).v
synth: $(BUILD)/$(TOP).json
pnr: $(BUILD)/$(TOP).pnr.json
bitstream: $(BUILD)/$(TOP).fs

$(BUILD)/$(TOP).v: $(SOURCES)
	dotnet run --project src/MidiSynth.Emit -- $@

$(BUILD)/$(TOP).json: $(BUILD)/$(TOP).v
	cd $(BUILD) && $(YOSYS) -q -l synth.log -p "read_verilog $(TOP).v; synth_gowin -top $(TOP) -json $(TOP).json -family gw5a -strict-gw5a-dffs -nolutram -setundef"

$(BUILD)/$(TOP).pnr.json: $(BUILD)/$(TOP).json $(CST)
	$(NEXTPNR) --timing-allow-fail -r --top $(TOP) --json $< --write $@ \
		--device $(DEVICE) --vopt cst=$(CST) --vopt sspi_as_gpio --log $(BUILD)/pnr.log -q

$(BUILD)/$(TOP).fs: $(BUILD)/$(TOP).pnr.json
	$(GOWIN_PACK) --sspi_as_gpio --cpu_as_gpio --ready_as_gpio -d $(FAMILY) -o $@ $<

load: $(BUILD)/$(TOP).fs
	$(LOADER) -b tangprimer25k $<

clean:
	rm -rf $(BUILD)
