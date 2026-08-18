Remove-Item -Path build\Pomodoro\*.pdb -Force
Compress-Archive -Path build\Pomodoro -DestinationPath "build\pomodoro_${env:VERSION}.${env:RUNTIME}.zip" -Force
