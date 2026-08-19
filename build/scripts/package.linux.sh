#!/usr/bin/env bash

set -e
set -o pipefail

# ICU versions to support (Debian has no virtual package, must list all)
ICU_VERSIONS="78 77 76 74 72 71 70 69 68 67 66 65 63"

arch=
appimage_arch=
target=
case "$RUNTIME" in
    linux-x64)
        arch=amd64
        appimage_arch=x86_64
        target=x86_64;;
    linux-arm64)
        arch=arm64
        appimage_arch=arm_aarch64
        target=aarch64;;
    *)
        echo "Unknown runtime $RUNTIME"
        exit 1;;
esac

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage

cd build

if [[ ! -f "appimagetool" ]]; then
    curl -o appimagetool -L "$APPIMAGETOOL_URL"
    chmod +x appimagetool
fi

rm -f Pomodoro/*.dbg
rm -f Pomodoro/*.pdb

# --- AppImage ---
mkdir -p Pomodoro.AppDir/opt
mkdir -p Pomodoro.AppDir/usr/share/metainfo
mkdir -p Pomodoro.AppDir/usr/share/applications

cp -r Pomodoro Pomodoro.AppDir/opt/pomodoro
desktop-file-install resources/_common/applications/pomodoro.desktop --dir Pomodoro.AppDir/usr/share/applications \
    --set-icon com.pomodoro.Pomodoro --set-key=Exec --set-value=AppRun
mv Pomodoro.AppDir/usr/share/applications/{pomodoro,com.pomodoro.Pomodoro}.desktop
cp resources/_common/icons/pomodoro.png Pomodoro.AppDir/com.pomodoro.Pomodoro.png
ln -rsf Pomodoro.AppDir/opt/pomodoro/pomodoro Pomodoro.AppDir/AppRun
ln -rsf Pomodoro.AppDir/usr/share/applications/com.pomodoro.Pomodoro.desktop Pomodoro.AppDir
cp resources/appimage/pomodoro.appdata.xml Pomodoro.AppDir/usr/share/metainfo/com.pomodoro.Pomodoro.appdata.xml

ARCH="$appimage_arch" ./appimagetool -v Pomodoro.AppDir "pomodoro-$VERSION.linux.$arch.AppImage"

# --- DEB ---
mkdir -p resources/deb/opt/pomodoro/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons/hicolor/256x256/apps
cp -rf Pomodoro/* resources/deb/opt/pomodoro
ln -rsf resources/deb/opt/pomodoro/pomodoro resources/deb/usr/bin
cp resources/_common/applications/pomodoro.desktop \
   resources/deb/usr/share/applications/com.pomodoro.Pomodoro.desktop
cp resources/_common/icons/pomodoro.png \
   resources/deb/usr/share/icons/hicolor/256x256/apps/com.pomodoro.Pomodoro.png

installed_size=$(du -sk resources/deb | cut -f1)

icu_deps="libicu"
for v in $ICU_VERSIONS; do
    icu_deps="$icu_deps | libicu$v"
done

sed -i -e "s/^Version:.*/Version: $VERSION/" \
    -e "s/^Architecture:.*/Architecture: $arch/" \
    -e "s/^Installed-Size:.*/Installed-Size: $installed_size/" \
    -e "s/@ICU_DEPS@/$icu_deps/" \
    resources/deb/DEBIAN/control

dpkg-deb -Zgzip --root-owner-group --build resources/deb "pomodoro_${VERSION}-1_${arch}.deb"

# --- RPM ---
# Prepare RPM build directory with the built binaries
mkdir -p resources/rpm/BUILD/Pomodoro
cp -r Pomodoro/* resources/rpm/BUILD/Pomodoro/

# Copy resources needed by the spec file
cp -r resources/_common resources/rpm/BUILD/
cp -r resources/appimage resources/rpm/BUILD/

rpmbuild -bb --target="$target" resources/rpm/SPECS/build.spec --define "_topdir $(pwd)/resources/rpm" --define "_version $VERSION"
mv "resources/rpm/RPMS/$target/pomodoro-$VERSION-1.$target.rpm" ./