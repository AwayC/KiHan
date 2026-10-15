#!/usr/bin/env bash
# 逻辑层命令行测试：编译检查 + 全部测试工程，任一失败即返回非零
# 用法：Tests/LogicTests/run_all.sh        (需要 .NET 8 SDK，不需要 Unity)
set -u
cd "$(dirname "$0")"
fail=0

echo "## Compile (netstandard2.1)"
out=$(dotnet build Compile/Compile.csproj -nologo -v q 2>&1); code=$?
echo "$out" | grep -E " error | warning |Warning\(s\)|Error\(s\)" || true
[ $code -ne 0 ] && { echo "  [FAIL] 编译失败"; fail=1; }

for t in Combat Skill Jump HitGC Hurt Stride; do
  echo "## $t"
  out=$(dotnet run --project "$t/$t.csproj" -nologo -v q 2>&1); code=$?
  echo "$out" | grep -E "\[FAIL\]|全部通过|失败 [0-9]+ 项|same=|托管分配|Exception" || true
  if [ $code -ne 0 ] || echo "$out" | grep -qE "\[FAIL\]|Unhandled exception"; then
    echo "  [FAIL] $t (exit $code)"; fail=1
  fi
done

[ $fail -eq 0 ] && echo "ALL PASSED" || echo "SOME TESTS FAILED"
exit $fail
