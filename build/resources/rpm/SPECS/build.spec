Name:           pomodoro
Version:        %{_version}
Release:        1%{?dist}
Summary:        A minimal, focused Pomodoro timer
License:        MIT
URL:            https://github.com/pomodoro/pomodoro-app

Requires:       glibc >= 2.31
Requires:       libstdc++
Requires:       libicu

%description
A cross-platform Pomodoro timer built with .NET and Avalonia UI.
Features task management, configurable durations, native notifications,
activity tracking during breaks, and beautiful daily reports.

%install
mkdir -p %{buildroot}/opt/pomodoro
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps

cp -r %{_builddir}/Pomodoro/* %{buildroot}/opt/pomodoro/

ln -sf /opt/pomodoro/pomodoro %{buildroot}/usr/bin/pomodoro

cp %{_builddir}/_common/applications/pomodoro.desktop \
   %{buildroot}/usr/share/applications/com.pomodoro.Pomodoro.desktop

cp %{_builddir}/_common/icons/pomodoro.png \
   %{buildroot}/usr/share/icons/hicolor/256x256/apps/com.pomodoro.Pomodoro.png

%files
/opt/pomodoro/*
/usr/bin/pomodoro
/usr/share/applications/com.pomodoro.Pomodoro.desktop
/usr/share/icons/hicolor/256x256/apps/com.pomodoro.Pomodoro.png