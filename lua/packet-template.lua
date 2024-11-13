alis = Proto("template",  "template")

alis.fields = { } --Fields should added in this set

function alis.dissector(buffer, pinfo, tree)

  length = buffer:len() --Payload length could be useful
  pinfo.cols.protocol = alis.name --Sets Protocol name column

  local subtree = tree:add(alis, buffer(), "template")


end

local udp_port = DissectorTable.get("udp.port")
udp_port:add(12345, alis) --Adds dissector to DissectorTable with port 12345
--Heuristic dissectors are possible too
