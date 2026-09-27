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
  inputs: StepInput[];
};

type RecipeDetail = { id: number; name: string; steps: Step[] };

type MachineOption = { id: number; name: string };

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

  const fetchRecipes = async () => {
    const res = await fetch("/api/recipes");
    setRecipes(await res.json());
  };

  const fetchDetail = async (id: number) => {
    const res = await fetch(`/api/recipes/${id}`);
    if (!res.ok) {
      setDetail(null);
      return;
    }
    setDetail(await res.json());
  };

  useEffect(() => {
    fetchRecipes();
    fetch("/api/machines")
      .then((r) => r.json())
      .then((data: MachineOption[]) => setMachines(data));
  }, []);

  useEffect(() => {
    if (selectedId !== null) {
      fetchDetail(selectedId);
    } else {
      setDetail(null);
    }
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

          <table border={1} cellPadding={6} style={{ borderCollapse: "collapse", marginBottom: 16 }}>
            <thead>
              <tr>
                <th>순서</th>
                <th>기계</th>
                <th>동작</th>
                <th>시간(분)</th>
                <th>온도(°C)</th>
                <th>재료</th>
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
