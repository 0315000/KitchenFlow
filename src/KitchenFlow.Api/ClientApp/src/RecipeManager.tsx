import { useEffect, useRef, useState } from "react";

// T4: 레시피와 조리 단계 화면
// - 레시피 목록 / 생성 / 삭제
// - 레시피 상세: 단계 목록 + 추가 / 삭제 / 순서 변경

type RecipeSummary = { id: number; name: string; stepCount: number };

type StepInput = {
  ingredientId: number;
  ingredientName: string;
  unit: string;
  quantity: number;
};

type Step = {
  id: number;
  order: number;
  machineId: number;
  machineName: string;
  action: string;
  durationMinutes: number;
  tempC: number | null;
  dependsOn: number[]; // 이 단계보다 먼저 끝나야 하는 단계 Id
  inputs: StepInput[];
};

// 조리 흐름(병렬) 계산 결과
type ScheduledStep = {
  id: number;
  order: number;
  action: string;
  machineId: number;
  machineName: string;
  durationMinutes: number;
  startMinute: number;
  endMinute: number;
};

type Schedule = {
  totalMinutes: number;
  sequentialMinutes: number;
  steps: ScheduledStep[];
};
// T6: 순차 실행 시뮬레이션 (POST /api/recipes/{id}/simulate)
type TimelineEntry = {
  machineId: number; // T8: 어느 기계에서 하는지 (기계별 레인용)
  stepId: number;    // 어느 단계인지 (Step의 Id)
  startMin: number;  // 몇 분에 시작하나
  endMin: number;    // 몇 분에 끝나나
  isPreheat?: boolean; // T10: stepId 단계를 위한 오븐 예열 구간 (예열 규칙 전 실행 기록에는 없음)
};
// T9: 실행 이력 한 줄 (GET /api/recipes/{id}/runs)
type RunSummary = {
  id: number;
  startedAt: string;            // 실행 시각 (서버가 UTC로 저장)
  succeeded: boolean;
  failureReason: string | null; // 성공이면 null
  totalMinutes: number;         // 실패면 0
};
// T9: 실행 1건 상세 (GET /api/recipes/{id}/runs/{runId}) — 목록 한 줄 + 그때 저장된 막대들
type RunDetail = RunSummary & {
  events: TimelineEntry[]; // 시뮬레이션 막대와 같은 모양 (stepId, machineId, startMin, endMin)
};
const machineColors = ["#4e79a7", "#f28e2b", "#59a14f", "#e15759", "#76b7b2", "#b07aa1", "#edc948"];
const colorOf = (machineId: number) => machineColors[machineId % machineColors.length];
// 12.5분 → "12:30" (분:초)
const formatMin = (min: number) => {
  const totalSec = Math.floor(min * 60);
  return `${Math.floor(totalSec / 60)}:${String(totalSec % 60).padStart(2, "0")}`;
};
// T9: 서버 시각(UTC인데 끝에 Z가 없음) → 한국 시각 "2026. 10. 16. 오후 3:41:38"
const formatRunTime = (utc: string) => new Date(utc.endsWith("Z") ? utc : utc + "Z").toLocaleString("ko-KR");
type RecipeDetail = { id: number; name: string; steps: Step[] };
type MachineOption = { id: number; name: string };

// T5: 실행 전 검사 결과 (POST /api/recipes/{id}/validate)
type Violation = {
  code: string;
  message: string;
  stepId: number | null; // 레시피 전체 위반(빈 레시피)은 null
  order: number | null;
};

type ValidationResult = { recipeId: number; isValid: boolean; violations: Violation[] };

// 배지에 보여 줄 짧은 이름 (전체 설명은 마우스를 올리면 보인다)
const violationLabels: Record<string, string> = {
  EMPTY_RECIPE: "빈 레시피",
  INVALID_DURATION: "소요시간",
  OUT_OF_STOCK: "재고 부족",
  UNKNOWN_MACHINE: "없는 기계",
  OVER_CAPACITY: "용량 초과",
  TEMP_OUT_OF_RANGE: "온도 범위",
};

// 빨간 배지 한 개
const ViolationBadge = ({ v }: { v: Violation }) => (
  <span
    title={v.message}
    style={{
      display: "inline-block",
      margin: 2,
      padding: "2px 8px",
      borderRadius: 10,
      background: "#d93025",
      color: "white",
      fontSize: 12,
      whiteSpace: "nowrap",
    }}
  >
    {violationLabels[v.code] ?? v.code}
  </span>
);
const emptyStepForm = { machineId: 0, action: "", durationMinutes: 1, tempC: "" };

function RecipeManager() {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [machines, setMachines] = useState<MachineOption[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<RecipeDetail | null>(null);
  const [newRecipeName, setNewRecipeName] = useState("");
  const [stepForm, setStepForm] = useState(emptyStepForm);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false); // 연타 방지
  const [schedule, setSchedule] = useState<Schedule | null>(null);
  const [editingDepsId, setEditingDepsId] = useState<number | null>(null);
  const [draftDeps, setDraftDeps] = useState<number[]>([]);
  const [validation, setValidation] = useState<ValidationResult | null>(null);
  const [timeline, setTimeline] = useState<TimelineEntry[] | null>(null);
  const [runs, setRuns] = useState<RunSummary[]>([]); // T9: 실행 이력 목록
  // T6 재생: 지금 몇 분째인지 / 재생 중인지 / 배속 (서버가 아니라 화면이 기억한다 — DP4 결정 B)
  const [selectedRun, setSelectedRun] = useState<RunDetail | null>(null); // T9: 표에서 고른 실행 1건 (상세)
  const [currentMin, setCurrentMin] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(60);
  const fetchRecipes = async () => {
    const res = await fetch("/api/recipes");
    setRecipes(await res.json());
  };

  // T8 몽키 테스트: 레시피를 빠르게 바꿀 때 늦게 도착한 이전 응답이 화면을 덮어쓰지 않도록,
  //요청마다 번호표를 주고 가장 최근 번호표의 응답만 화면에 넣는다.
  const latestDetailRequest = useRef(0);
  const fetchDetail = async (id: number) => {
    const ticket = ++latestDetailRequest.current; // 이번 요청의 번호표

      // 5개를 동시에 요청하고, 다 모이면 한 번에 화면에 넣는다 (중간에 섞일 틈이 없게)
    const json = (r: Response) => (r.ok ? r.json() : Promise.resolve(null));
    const [detailData, scheduleData, validateData, simulateData, runsData] = await Promise.all([
      
      fetch(`/api/recipes/${id}`).then(json),
      fetch(`/api/recipes/${id}/schedule`).then(json),
      fetch(`/api/recipes/${id}/validate`, { method: "POST" }).then(json),
      fetch(`/api/recipes/${id}/simulate`, { method: "POST" }).then(json),
      fetch(`/api/recipes/${id}/runs`).then(json),
    ]);

    if (ticket !== latestDetailRequest.current) return; // 그 사이 다른 레시피를 눌렀으면 이 응답은 버린다

    setDetail(detailData);
    setSchedule(detailData ? scheduleData : null);
    setValidation(detailData ? validateData : null);
    setTimeline(detailData ? simulateData : null);
    setRuns(detailData ? runsData ?? [] : []);                                                    };

  useEffect(() => {
    fetchRecipes();
    fetch("/api/machines")
      .then((r) => r.json())
      .then((data: MachineOption[]) => setMachines(data));
  }, [selectedId]);
  useEffect(() => {
    if (selectedId !== null) {
      fetchDetail(selectedId);
    } else {
      latestDetailRequest.current++;
      setDetail(null);
      setSchedule(null);
      setValidation(null);
      setTimeline(null);
      setRuns([]);
    }
    setEditingDepsId(null);
    setSelectedRun(null);
    setPlaying(false);
    setCurrentMin(0);
  }, [selectedId]);

  // 공통 호출: 연타 방지 + 서버 에러 메시지 표시 + 목록/상세 다시 불러오기
  const call = async (url: string, method: string, body?: object) => {
    if (busy) return false;
    setBusy(true);
    setErrorMessage(null);
    try {
      const res = await fetch(url, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      });
      if (!res.ok) {
        const text = await res.text();
        setErrorMessage(text || "요청 처리 중 오류가 발생했습니다.");
        return false;
      }
      return true;
    } finally {
      await fetchRecipes();
      if (selectedId !== null) {
        await fetchDetail(selectedId);
      }
      setBusy(false);
    }
  };
  // T9: 레시피 실행 → 서버가 재고 차감 + 기록 저장. 끝나면 call 이 상세(실행 이력 포함)를 다시 불러온다
  const handleRun = async () => {
    if (selectedId === null) return;
    await call(`/api/recipes/${selectedId}/runs`, "POST");
  };
  // T9: 실행 이력 표에서 한 줄을 누르면 그 실행의 상세(막대 포함)를 불러온다
  const handleSelectRun = async (runId: number) => {
    if (selectedId === null) return;
    const res = await fetch(`/api/recipes/${selectedId}/runs/${runId}`);
    setSelectedRun(res.ok ? await res.json() : null);
    setPlaying(false); // 다른 실행을 고르면 재생을 멈추고 처음부터
    setCurrentMin(0);
  };
  const handleCreateRecipe = async (e: { preventDefault: () => void }) => {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setErrorMessage(null);
    const res = await fetch("/api/recipes", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: newRecipeName }),
    });
    if (!res.ok) {
      setErrorMessage((await res.text()) || "레시피 생성 중 오류가 발생했습니다.");
      setBusy(false);
      return;
    }
    const created: { id: number } = await res.json();
    setNewRecipeName("");
    await fetchRecipes();
    setSelectedId(created.id);
    setBusy(false);
  };

  const handleDeleteRecipe = async (id: number) => {
    if (!window.confirm("레시피를 삭제하면 모든 조리 단계도 함께 삭제됩니다. 계속할까요?")) return;
    if (selectedId === id) setSelectedId(null);
    await call(`/api/recipes/${id}`, "DELETE");
  };

  const handleAddStep = async (e: { preventDefault: () => void }) => {
    e.preventDefault();
    if (selectedId === null) return;
    const ok = await call(`/api/recipes/${selectedId}/steps`, "POST", {
      machineId: stepForm.machineId,
      action: stepForm.action,
      durationMinutes: stepForm.durationMinutes,
      tempC: stepForm.tempC === "" ? null : Number(stepForm.tempC),
    });
    if (ok) {
      // 기계는 유지하고 나머지만 비워서 연속 입력을 편하게
      setStepForm((prev) => ({ ...emptyStepForm, machineId: prev.machineId }));
    }
  };

  // ---- 선행 단계 편집 (병렬 조리) ----
  const startEditDeps = (s: Step) => {
    setEditingDepsId(s.id);
    setDraftDeps(s.dependsOn);
  };

  const toggleDraftDep = (id: number) => {
    setDraftDeps((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  };

  const saveDeps = async () => {
    if (detail === null || editingDepsId === null) return;
    const ok = await call(`/api/recipes/${detail.id}/steps/${editingDepsId}/dependencies`, "PUT", {
      dependsOnStepIds: draftDeps,
    });
    if (ok) setEditingDepsId(null);
  };

  const orderOf = (id: number) => detail?.steps.find((x) => x.id === id)?.order;

  // 간트 차트 1분당 픽셀 (전체가 약 600px 안에 들어오게)
  const pxPerMinute = schedule && schedule.totalMinutes > 0 ? Math.min(12, 600 / schedule.totalMinutes) : 12;
  // T9 할 일 4: 베스트 기록 = 성공한 실행 중 총 소요시간이 가장 짧은 것 (동점이면 먼저 세운 기록 = 번호가 작은 쪽)
  const successRuns = runs.filter((r) => r.succeeded);
  const bestRun =
    successRuns.length === 0
      ? null
      : successRuns.reduce((best, r) =>
          r.totalMinutes < best.totalMinutes || (r.totalMinutes === best.totalMinutes && r.id < best.id) ? r : best
        );  
  // T9: 실행 이력에서 고른 실행이 있으면 그 실행의 막대를, 없으면 시뮬레이션 막대를 아래 간트·재생·스코어에 쓴다
  const shown = selectedRun ? selectedRun.events : timeline;
  // (병렬이면 배열의 마지막 항목이 가장 늦게 끝난다는 보장이 없다)
  const simTotal = shown && shown.length > 0 ? Math.max(...shown.map((t) => t.endMin)) : 0;
  const simPx = simTotal > 0 ? 600 / simTotal : 12; // T8: 레시피 길이와 상관없이 간트 폭을 600px 로 맞춘다 (짧은 레시피도 막대 이름이 보이게)
  // T8: 기계별 레인 — 같은 기계를 쓰는 단계들을 한 줄에 모은다 (기계가 처음 나온 순서대로)
  const lanes = shown
    ? [...new Set(shown.map((t) => t.machineId))].map((machineId) => ({
        machineId,
        entries: shown.filter((t) => t.machineId === machineId),
      }))
    : [];
  // T8: 스코어 — 총 소요시간 / 기계 유휴율 / 재료 낭비량 → 별 3개
  const endOfStep = new Map((shown ?? []).filter((t) => !t.isPreheat).map((t) => [t.stepId, t.endMin])); // 단계 Id → 끝 시각 (예열 막대 제외)
  // ① 총 소요시간: 순차 실행(모든 단계 시간 합 + T10 예열 시간 합) 대비 얼마나 줄었나
  const preheatTotal = (shown ?? []).filter((t) => t.isPreheat).reduce((sum, t) => sum + (t.endMin - t.startMin), 0);
  const seqTotal = detail ? detail.steps.reduce((sum, s) => sum + s.durationMinutes, 0) + preheatTotal : 0;
  const shortenPct = seqTotal > 0 ? Math.round((1 - simTotal / seqTotal) * 100) : 0;
  // ② 기계 유휴율: 쓰는 기계들의 전체 시간 중 놀고 있는 비율
  const busyTotal = (shown ?? []).reduce((sum, t) => sum + (t.endMin - t.startMin), 0);
  const idlePct = lanes.length > 0 && simTotal > 0 ? Math.round((1 - busyTotal / (lanes.length * simTotal)) * 100) : 0;
  // ③ 재료 낭비량: 재료가 들어간 단계가 준비된 뒤 기계를 기다린 시간 합 (손질한 재료가 방치된 시간)
  const wasteMin = (shown ?? []).reduce((sum, t) => {
    if (t.isPreheat) return sum;                                                  // T10: 예열은 재료를 쓰지 않는다
    const step = detail?.steps.find((s) => s.id === t.stepId);
    if (!step || step.inputs.length === 0) return sum;                            // 재료가 없는 단계는 제외
    const readyAt = Math.max(0, ...step.dependsOn.map((d) => endOfStep.get(d) ?? 0)); // 선행이 다 끝난 시각
    return sum + (t.startMin - readyAt);                                           // 준비됐는데 기다린 시간
  }, 0);
  // 별: 기준을 통과한 개수 (단축률 20% 이상 / 유휴율 30% 이하 / 방치 0분)
  const stars = [shortenPct >= 20, idlePct <= 30, wasteMin === 0].filter(Boolean).length;
  // T6 재생: 재생 중이면 0.1초마다z (흐른 시간 × 배속)만큼 커서를 앞으로
  useEffect(() => {
    if (!playing) return;                        // 멈춤이면 타이머를 켜지 않음
    const timer = setInterval(() => {
      setCurrentMin((prev) => {
        const next = prev + (0.1 * speed) / 60;  // 0.1초 동안 흐른 "레시피 분"
        if (next >= simTotal) {                  // 끝까지 갔으면
          setPlaying(false);                     //   멈추고
          return simTotal;                       //   커서는 마지막 시각에 고정
        }
        return next;
      });
    }, 100);                                     // 100ms = 0.1초마다 실행
    return () => clearInterval(timer);           // 멈추거나 배속이 바뀌면 이전 타이머 정리
  }, [playing, speed, simTotal]);
  return (
    <div>
      <h1>레시피 관리</h1>

      <form onSubmit={handleCreateRecipe} style={{ marginBottom: 16 }}>
        <input
          placeholder="새 레시피 이름"
          value={newRecipeName}
          onChange={(e) => setNewRecipeName(e.target.value)}
        />
        <button type="submit" disabled={busy}>레시피 추가</button>
      </form>

      {errorMessage && <p style={{ color: "red" }}>{errorMessage}</p>}

      <table border={1} cellPadding={6} style={{ borderCollapse: "collapse", marginBottom: 24 }}>
        <thead>
          <tr>
            <th>레시피</th>
            <th>단계 수</th>
            <th>작업</th>
          </tr>
        </thead>
        <tbody>
          {recipes.map((r) => (
            <tr key={r.id} style={r.id === selectedId ? { background: "#eef" } : undefined}>
              <td>{r.name}</td>
              <td>{r.stepCount}</td>
              <td>
                <button onClick={() => setSelectedId(r.id)}>상세</button>
                <button disabled={busy} onClick={() => handleDeleteRecipe(r.id)}>삭제</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {detail && (
        <div>
          <h2>{detail.name} — 조리 단계</h2>
          {validation && (
            <p>
              {validation.isValid ? (
                <span style={{ color: "green" }}>✔ 실행 가능한 레시피</span>
              ) : (
                <>
                  <b style={{ color: "#d93025" }}>실행 불가 — 위반 {validation.violations.length}건</b>{" "}
                  {validation.violations
                    .filter((v) => v.stepId === null)
                    .map((v, i) => <ViolationBadge key={i} v={v} />)}
                </>
              )}
            </p>
          )}
          <table border={1} cellPadding={6} style={{ borderCollapse: "collapse", marginBottom: 16 }}>
            <thead>
              <tr>
                <th>순서</th>
                <th>기계</th>
                <th>동작</th>
                <th>시간(분)</th>
                <th>온도(°C)</th>
                <th>재료</th>
                <th>선행 단계</th>
                <th>검사</th>
                <th>작업</th>
              </tr>
            </thead>
            <tbody>
              {detail.steps.map((s, index) => (
                <tr key={s.id}>
                  <td>{s.order}</td>
                  <td>{s.machineName}</td>
                  <td>{s.action}</td>
                  <td>{s.durationMinutes}</td>
                  <td>{s.tempC ?? "-"}</td>
                  <td>
                    {s.inputs.length === 0
                      ? "-"
                      : s.inputs.map((i) => `${i.ingredientName} ${i.quantity}${i.unit}`).join(", ")}
                  </td>
                  <td>
                    {editingDepsId === s.id ? (
                      <div style={{ textAlign: "left" }}>
                        {detail.steps
                          .filter((o) => o.id !== s.id)
                          .map((o) => (
                            <label key={o.id} style={{ display: "block", whiteSpace: "nowrap" }}>
                              <input
                                type="checkbox"
                                checked={draftDeps.includes(o.id)}
                                onChange={() => toggleDraftDep(o.id)}
                              />
                              {o.order}. {o.action}
                            </label>
                          ))}
                        <button disabled={busy} onClick={saveDeps}>저장</button>
                        <button onClick={() => setEditingDepsId(null)}>취소</button>
                      </div>
                    ) : (
                      <>
                        {s.dependsOn.length === 0
                          ? "바로 시작"
                          : s.dependsOn
                              .map((id) => orderOf(id))
                              .sort((a, b) => (a ?? 0) - (b ?? 0))
                              .join(", ") + "번 후"}
                        <br />
                        <button disabled={busy} onClick={() => startEditDeps(s)}>변경</button>
                      </>
                    )}
                  </td>
                  <td>
                    {(() => {
                      const mine = validation?.violations.filter((v) => v.stepId === s.id) ?? [];
                      return mine.length === 0
                        ? <span style={{ color: "green" }}>✔</span>
                        : mine.map((v, i) => <ViolationBadge key={i} v={v} />);
                    })()}
                  </td>
                  <td>
                    <button
                      disabled={busy || index === 0}
                      onClick={() => call(`/api/recipes/${detail.id}/steps/${s.id}/move?direction=up`, "POST")}
                    >
                      ▲
                    </button>
                    <button
                      disabled={busy || index === detail.steps.length - 1}
                      onClick={() => call(`/api/recipes/${detail.id}/steps/${s.id}/move?direction=down`, "POST")}
                    >
                      ▼
                    </button>
                    <button
                      disabled={busy}
                      onClick={() => {
                        // 실수로 지우지 않도록 한 번 확인한다 (레시피 삭제와 같은 방식)
                        if (!window.confirm(`${s.order}번 "${s.action}" 단계를 삭제할까요?`)) return;
                        call(`/api/recipes/${detail.id}/steps/${s.id}`, "DELETE");
                      }}
                    >
                      삭제
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {schedule && schedule.steps.length > 0 && (
            <div style={{ margin: "24px 0", textAlign: "left" }}>
              <h3 style={{ textAlign: "center" }}>조리 흐름 — 동시에 진행해서 한 요리로 완성 (예열 미반영)</h3>
              <p style={{ textAlign: "center" }}>
                동시에 진행하면 <b>{schedule.totalMinutes}분</b> 만에 완성
                {schedule.sequentialMinutes > schedule.totalMinutes && (
                  <> (하나씩 하면 {schedule.sequentialMinutes}분 → <b>{schedule.sequentialMinutes - schedule.totalMinutes}분 단축</b>)</>
                )}
              </p>

              {schedule.steps.map((st) => (
                <div key={st.id} style={{ display: "flex", alignItems: "center", marginBottom: 4 }}>
                  <div style={{ width: 170, fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {st.order}. {st.action}
                  </div>
                  <div style={{ position: "relative", height: 22, width: schedule.totalMinutes * pxPerMinute + 60 }}>
                    <div
                      title={`${st.machineName} · ${st.startMinute}~${st.endMinute}분`}
                      style={{
                        position: "absolute",
                        left: st.startMinute * pxPerMinute,
                        width: Math.max(st.durationMinutes * pxPerMinute, 4),
                        height: "100%",
                        background: colorOf(st.machineId),
                        borderRadius: 3,
                      }}
                    />
                    <span
                      style={{
                        position: "absolute",
                        left: st.endMinute * pxPerMinute + 4,
                        fontSize: 11,
                        lineHeight: "22px",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {st.machineName} {st.startMinute}~{st.endMinute}분
                    </span>
                  </div>
                </div>
              ))}

              <div style={{ display: "flex", alignItems: "center", marginTop: 6 }}>
                <div style={{ width: 170, fontSize: 13, fontWeight: "bold" }}>🍽 완성</div>
                <div style={{ position: "relative", height: 22 }}>
                  <div
                    style={{
                      position: "absolute",
                      left: schedule.totalMinutes * pxPerMinute,
                      width: 2,
                      height: "100%",
                      background: "#333",
                    }}
                  />
                  <span style={{ position: "absolute", left: schedule.totalMinutes * pxPerMinute + 6, fontSize: 12, whiteSpace: "nowrap", lineHeight: "22px" }}>
                    {schedule.totalMinutes}분
                  </span>
                </div>
              </div>
              <p style={{ fontSize: 12, color: "#666" }}>
                같은 시간대에 막대가 겹치면 동시에 진행되는 단계입니다. 같은 기계는 한 번에 한 단계만 쓸 수 있어요.
              </p>
            </div>
          )}
          {shown && shown.length > 0 && (
            <div style={{ margin: "24px 0", textAlign: "left" }}>
              <h3 style={{ textAlign: "center" }}>
                {selectedRun
                  ? `#${selectedRun.id} 실행 다시 보기 — ${formatRunTime(selectedRun.startedAt)}`
                  : "의존관계 시뮬레이션 — 선행 단계가 끝나면 시작"}
              </h3>
              {selectedRun && (
                <p style={{ textAlign: "center" }}>
                  <button onClick={() => setSelectedRun(null)}>↩ 시뮬레이션으로 돌아가기</button>
                </p>
              )}
              <p style={{ textAlign: "center" }}>
                완성까지 <b>{simTotal}분</b> (선행이 없는 단계는 동시에 시작)
              </p>
              {/* T8: 스코어 — 기준 통과 개수만큼 ★ (마우스를 올리면 기준 설명) */}
              <p
                style={{ textAlign: "center" }}
                title="★ 단축률 20% 이상 / ★ 기계 유휴율 30% 이하 / ★ 재료 방치 0분"
              >
                스코어 <b style={{ color: "#f2a900", fontSize: 18 }}>{"★".repeat(stars)}{"☆".repeat(3 - stars)}</b>{" "}
                · 총 {simTotal}분 (순차 {seqTotal}분 대비 {shortenPct}% 단축) · 기계 유휴율 {idlePct}% · 재료 방치 {wasteMin}분
              </p>
              <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8, flexWrap: "wrap" }}>
                {playing ? (
                  <button onClick={() => setPlaying(false)}>⏸ 일시정지</button>
                ) : (
                  <button
                    onClick={() => {
                      if (currentMin >= simTotal) setCurrentMin(0); // 끝난 뒤 다시 누르면 처음부터
                      setPlaying(true);
                    }}
                  >
                    ▶ 재생
                  </button>
                )}
                <button
                  onClick={() => {
                    setPlaying(false);
                    setCurrentMin(0);
                  }}
                >
                  ↺ 처음으로
                </button>
                <span style={{ marginLeft: 8 }}>배속</span>
                {[1, 10, 60].map((s) => (
                  <button
                    key={s}
                    onClick={() => setSpeed(s)}
                    style={s === speed ? { fontWeight: "bold", background: "#333", color: "white" } : undefined}
                  >
                    {s}x
                  </button>
                ))}
                <span style={{ marginLeft: 8 }}>
                  <b>{formatMin(currentMin)}</b> / {formatMin(simTotal)}
                </span>
              </div>
              <div style={{ position: "relative" }}>
                {lanes.map((lane) => {
                  const machineName = detail.steps.find((s) => s.machineId === lane.machineId)?.machineName;
                  return (
                    <div key={lane.machineId} style={{ display: "flex", alignItems: "center", marginBottom: 4 }}>
                      {/* 왼쪽 이름 칸: 단계 이름 대신 기계 이름 */}
                      <div style={{ width: 170, flexShrink: 0, fontSize: 13, fontWeight: "bold" }}>
                        {machineName}
                      </div>
                      {/* 회색 바탕 = 기계가 놀고 있는(유휴) 시간, 색 막대 = 일하는 시간 */}
                      <div style={{ position: "relative", height: 22, width: simTotal * simPx, flexShrink: 0, background: "#eee", borderRadius: 3 }}>
                        {lane.entries.map((t) => {
                          const step = detail.steps.find((s) => s.id === t.stepId);
                          // T10: 예열 막대는 굽기와 구분되게 빗금 + "예열" 이름 (같은 stepId 의 굽기 바로 앞)
                          const label = t.isPreheat ? "예열" : step?.action;
                          return (
                            <div
                              key={`${t.stepId}-${t.isPreheat ? "preheat" : "step"}`}
                              title={t.isPreheat
                                ? `예열 (${step?.order}. ${step?.action} ${step?.tempC ?? ""}°C 준비) · ${t.startMin}~${t.endMin}분`
                                : `${step?.order}. ${step?.action} · ${t.startMin}~${t.endMin}분`}
                              style={{
                                position: "absolute",
                                left: t.startMin * simPx,
                                width: (t.endMin - t.startMin) * simPx,
                                height: "100%",
                                background: t.isPreheat
                                  ? `repeating-linear-gradient(45deg, ${colorOf(t.machineId)}, ${colorOf(t.machineId)} 4px, #f6b26b 4px, #f6b26b 8px)`
                                  : colorOf(t.machineId),
                                border: "1px solid white",
                                boxSizing: "border-box",
                                borderRadius: 3,
                                color: "white",
                                fontSize: 11,
                                lineHeight: "20px",
                                overflow: "hidden",
                                whiteSpace: "nowrap",
                                paddingLeft: 3,
                              }}
                            >
                              {label}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
                {/* 현재 시각 커서 */}
                <div
                  style={{
                    position: "absolute",
                    top: 0,
                    bottom: 0,
                    left: 170 + currentMin * simPx,
                    width: 2,
                    background: "#d93025",
                    pointerEvents: "none",
                  }}
                />
              </div>
            </div>
          )}

          {/* T9: 실행 이력 */}
          <h3>
            실행 이력 ({runs.length}건){" "}
            <button onClick={handleRun} disabled={busy}>▶ 실행</button>
          </h3>
          {/* T9 할 일 4: 레시피별 베스트 기록 */}
          <p>
            🏆 베스트 기록:{" "}
            {bestRun
              ? `#${bestRun.id} · ${bestRun.totalMinutes}분 · ${formatRunTime(bestRun.startedAt)}`
              : "아직 성공한 실행이 없습니다."}
          </p>
          {runs.length === 0 ? (
          <p> 아직 실행한 기록이 없습니다.</p>
        ):(
        <table border={1} cellPadding={6} style={{ borderCollapse: "collapse", marginBottom: 24 }}>
        <thead>
          <tr>
          <th>#</th>
          <th>실행 시각</th>
          <th>결과</th>
          <th>소요(분)</th>
          <th>실패 사유</th>
          </tr>
          </thead>
          <tbody>
          {runs.map((r) => (
          <tr
            key={r.id}
            onClick={() => handleSelectRun(r.id)}
            style={{ cursor: "pointer", background: selectedRun?.id === r.id ? "#eef" : undefined }}
          >
                    <td>{r.id}{bestRun?.id === r.id && " 🏆"}</td>
                    <td>{formatRunTime(r.startedAt)}</td>
                    <td style={{ color: r.succeeded ? "#188038" : "#d93025" }}>{r.succeeded ? "✔ 성공" : "✘ 실패"}</td>
                    <td>{r.succeeded ? r.totalMinutes : "-"}</td>
                    <td>{r.failureReason ?? ""}</td>
                  </tr>
                ))}            
          </tbody>
          </table>
          )}          
{/* T9: 고른 실행이 성공이면 위쪽 간트에서 다시 보기 안내 */}
          {selectedRun && selectedRun.succeeded && (
            <p>#{selectedRun.id} 실행을 위쪽 간트에서 다시 보고 있어요. ▶ 재생으로 그때 진행을 볼 수 있어요.</p>
          )}
{/* T9: 고른 실행이 실패면 실패 사유  */}
          {selectedRun && !selectedRun.succeeded && (
            <p style={{ color: "#d93025" }}>
              ✘ #{selectedRun.id} 실행 실패 — {selectedRun.failureReason}
              <br />
              실제로 조리하지 않아 다시 볼 타임라인이 없어요.{" "}
              <button onClick={() => setSelectedRun(null)}>↩ 시뮬레이션으로 돌아가기</button>
            </p>
          )}

          <h3>단계 추가</h3>
          <form onSubmit={handleAddStep}>
            <select
              value={stepForm.machineId}
              onChange={(e) => setStepForm({ ...stepForm, machineId: Number(e.target.value) })}
            >
              <option value={0}>기계 선택</option>
              {machines.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
            <input
              placeholder="동작 (예: 굽기)"
              value={stepForm.action}
              onChange={(e) => setStepForm({ ...stepForm, action: e.target.value })}
            />
            <input
              type="number"
              style={{ width: 70 }}
              placeholder="분"
              value={stepForm.durationMinutes}
              onChange={(e) => setStepForm({ ...stepForm, durationMinutes: Number(e.target.value) })}
            />
            <input
              type="number"
              style={{ width: 90 }}
              placeholder="온도(선택)"
              value={stepForm.tempC}
              onChange={(e) => setStepForm({ ...stepForm, tempC: e.target.value })}
            />
            <button type="submit" disabled={busy}>추가</button>
          </form>
        </div>
      )}
    </div>
  );
}

export default RecipeManager;
