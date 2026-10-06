import { useEffect, useState } from "react";

// T4: 레시피와 조리 단계 화면
// - 레시피 목록 / 생성 / 삭제
// - 레시피 상세: 단계 목록 + 추가 / 삭제 / 순서 변경(▲▼)

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

const machineColors = ["#4e79a7", "#f28e2b", "#59a14f", "#e15759", "#76b7b2", "#b07aa1", "#edc948"];
const colorOf = (machineId: number) => machineColors[machineId % machineColors.length];

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

  const fetchRecipes = async () => {
    const res = await fetch("/api/recipes");
    setRecipes(await res.json());
  };

  const fetchDetail = async (id: number) => {
    const res = await fetch(`/api/recipes/${id}`);
    if (!res.ok) {
      setDetail(null);
      setSchedule(null);
      setValidation(null);
      return;
    }
    setDetail(await res.json());
    const scheduleRes = await fetch(`/api/recipes/${id}/schedule`);
    setSchedule(scheduleRes.ok ? await scheduleRes.json() : null);
    const validateRes = await fetch(`/api/recipes/${id}/validate`, { method: "POST" });
    setValidation(validateRes.ok ? await validateRes.json() : null);
  };

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
      setDetail(null);
      setSchedule(null);
      setValidation(null);
    }
    setEditingDepsId(null);
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
                      onClick={() => call(`/api/recipes/${detail.id}/steps/${s.id}`, "DELETE")}
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
              <h3 style={{ textAlign: "center" }}>조리 흐름 — 동시에 진행해서 한 요리로 완성</h3>
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
