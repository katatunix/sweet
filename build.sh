msbuild -t:Build -p:Configuration=Release -m:2

rm -dfr build
mkdir build
cp sweet/bin/Release/sweet.exe build
cp sweet/bin/Release/*.dll build
cp sweet/sweet.cfg build
cp stop/bin/Release/stop.exe build
