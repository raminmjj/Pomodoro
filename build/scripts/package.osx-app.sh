#!/usr/bin/env bash

set -e
set -o pipefail

cd build

mkdir -p Pomodoro.app/Contents/MacOS
mkdir -p Pomodoro.app/Contents/Resources
mv Pomodoro/* Pomodoro.app/Contents/MacOS/
cp resources/app/App.icns Pomodoro.app/Contents/Resources/App.icns
sed "s/POMODORO_VERSION/$VERSION/g" resources/app/App.plist > Pomodoro.app/Contents/Info.plist
rm -rf Pomodoro.app/Contents/MacOS/Pomodoro.dsym
rm -f Pomodoro.app/Contents/MacOS/*.pdb

zip "pomodoro_$VERSION.$RUNTIME.zip" -r Pomodoro.app
