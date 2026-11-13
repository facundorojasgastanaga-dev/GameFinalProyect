#!/bin/sh
# Usage: run_secondary_op.sh <op> [refresh]  -- queues an op for SecondaryShotgunAuthoring in the open Editor.
cd "$(dirname "$0")/../.."
R=Tools/IronRequiem/Reports/SecondaryShotgun
if [ "$2" = "refresh" ]; then
  id="secondary-shotgun-refresh-$(date +%s)"
  echo "{\"id\":\"$id\",\"operation\":\"refresh\"}" > Tools/IronRequiem/Requests/request.json
  sleep 15; rm -f Tools/IronRequiem/Requests/request.json
  n=0; while [ $n -lt 40 ]; do sleep 3; n=$((n+1)); tail -3 Logs/Editor.log | grep -q "Reloading assemblies\|ModeService\|entitlement\|Asset Pipeline Refresh\|Refreshing native" && break; done
  sleep 10
  grep -n "error CS" Logs/Editor.log | tail -5
fi
rm -f $R/status.txt
echo "$1" > Tools/IronRequiem/Requests/secondary-shotgun.txt
n=0; while [ ! -f $R/status.txt ] && [ $n -lt ${WAIT:-160} ]; do sleep 3; n=$((n+1)); done
cat $R/status.txt 2>/dev/null || echo "TIMEOUT waiting for $1"
