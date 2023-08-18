msbuild -t:Build -p:Configuration=Release -m:2

md build
copy sweet\bin\Release\sweet.exe build
copy sweet\bin\Release\*.dll build
copy sweet\sweet.cfg build
copy stop\bin\Release\stop.exe build
