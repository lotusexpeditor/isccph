alis = Proto("alis",  "ALIS")

local unk1 = ProtoField.uint8("alis.unk1", "unk1", base.HEX)
local timestamp = ProtoField.absolute_time("alis.time", "time",base.UTC)


alis.fields = { unk1, timestamp } --Fields should added in this set

function alis.dissector(buffer, pinfo, tree)

  length = buffer:len() --Payload length could be useful
  pinfo.cols.protocol = alis.name --Sets Protocol name column

  local subtree = tree:add(alis, buffer(), "ALIS")

  subtree:add(unk1,buffer(2,1))
  subtree:add_le(timestamp,buffer(length-4,4))

end

local udp_port = DissectorTable.get("udp.port")
udp_port:add(12345, alis) --Adds dissector to DissectorTable with port 12345
--Heuristic dissectors are possible too
