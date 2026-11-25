export PATH="$LOCALAPPDATA/Unity/bin:$PATH"
C="--caller plugin --skill unity-pipeline"
recompile(){ unity command script recompile $C >/dev/null 2>&1; for i in $(seq 1 60); do sleep 3; out=$(unity command script recompile_status $C 2>/dev/null); if echo "$out" | grep -q "completed\|up_to_date\|failed: true"; then echo "$out" | grep -v "^status"; break; fi; done; }
play(){ unity command editor.playmode start $C >/dev/null 2>&1; sleep 8; }
stop(){ unity command editor.playmode stop $C >/dev/null 2>&1; sleep 2; }
cat_(){ unity command script run --file AgentScripts/CatTest.cs --entry CatTest.$1 --args "$2" $C 2>&1 | head -1; }
stage(){ unity command script run --file AgentScripts/RunStage.cs --entry RunStage.Run --args "[\"PaperWings.EditorTools.$1\"]" $C 2>&1 | head -8; }
shot(){ cat_ Look "$2" >/dev/null; unity command capture game_view --source camera --camera AgentCam --overlay false --output-path Screenshots/$1.png $C >/dev/null 2>&1; }
