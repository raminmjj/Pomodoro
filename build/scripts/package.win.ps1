Remove-Item -Path build\SourceGit\*.pdb -Force
Compress-Archive -Path build\SourceGit -DestinationPath "build\pomodoro_${env:VERSION}.${env:RUNTIME}.zip" -Force
