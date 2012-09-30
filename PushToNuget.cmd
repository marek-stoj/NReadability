@echo off
cls & nant -buildfile:NReadability.build push-to-nuget
pause
