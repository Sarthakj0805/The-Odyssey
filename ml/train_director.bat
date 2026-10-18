@echo off
REM Train the AI Director with PPO (ML-Agents). Run from anywhere; uses the odyssey_ai conda env.
REM 1) double-click this file (or run it in cmd)   2) when it says "Listening on port 5004", press Play
REM    in Unity with the DirectorTraining scene open   3) when it finishes, the model is copied into Assets.
setlocal
set RUN_ID=%1
if "%RUN_ID%"=="" set RUN_ID=director_v1
cd /d "%~dp0.."
call "%USERPROFILE%\miniconda3\Scripts\activate.bat" odyssey_ai || (echo Could not activate conda env odyssey_ai & pause & exit /b 1)
python --version
mlagents-learn ml\configs\director_ppo.yaml --run-id=%RUN_ID% --results-dir=ml\results
if exist ml\results\%RUN_ID%\Director.onnx (
  if not exist Assets\ML\Models mkdir Assets\ML\Models
  copy /Y ml\results\%RUN_ID%\Director.onnx Assets\ML\Models\Director.onnx
  python ml\scripts\export_tensorboard.py --run ml\results\%RUN_ID% --out results\training
  echo.
  echo Model copied to Assets\ML\Models\Director.onnx. In Unity run: Odyssey ^> Assign Director Model To Scenes
)
pause
