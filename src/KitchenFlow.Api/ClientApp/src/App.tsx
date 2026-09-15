import { useEffect, useState } from "react";

type Machine = {
  id: number;
  name: string;
  kind: string;
  capacityMl: number;
  minTempC: number;
  maxTempC: number;
};

const emptyForm = {
  name: "",
  kind: "",
  capacityMl: 0,
  minTempC: 0,
  maxTempC: 0,
};

type InventoryItem = {
  id: number;
  itemName: string;
  quantity: number;
  expiryDate: string;
  threshold: number;
};

const todayIso = () => new Date().toISOString().slice(0, 10);

const emptyInventoryForm = {
  itemName: "",
  quantity: 0,
  expiryDate: todayIso(),
  threshold: 0,
};

function App() {
  const [machines, setMachines] = useState<Machine[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [inventoryItems, setInventoryItems] = useState<InventoryItem[]>([]);
  const [invForm, setInvForm] = useState(emptyInventoryForm);
  const [invEditingId, setInvEditingId] = useState<number | null>(null);
  const [invErrorMessage, setInvErrorMessage] = useState<string | null>(null);
  const [consumeAmounts, setConsumeAmounts] = useState<Record<number, string>>({});
  const [consumeErrorByItem, setConsumeErrorByItem] = useState<Record<number, string>>({});

  const fetchMachines = async () => {
    const res = await fetch("/api/machines");
    const data = await res.json();
    setMachines(data);
  };

  const fetchInventoryItems = async () => {
    const res = await fetch("/api/inventoryitems");
    const data = await res.json();
    setInventoryItems(data);
  };

  useEffect(() => {
    fetchMachines();
    fetchInventoryItems();
  }, []);

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const { name, value } = e.target;
    setForm((prev) => ({
      ...prev,
      [name]:
        name === "name" || name === "kind" ? value : Number(value),
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const isEdit = editingId !== null;
    const url = isEdit ? `/api/machines/${editingId}` : "/api/machines";
    const method = isEdit ? "PUT" : "POST";
    const body = isEdit ? { id: editingId, ...form } : form;

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const text = await res.text();
      setErrorMessage(text || "요청 처리 중 오류가 발생했습니다.");
      return;
    }

    setForm(emptyForm);
    setEditingId(null);
    fetchMachines();
  };

  const handleEdit = (m: Machine) => {
    setEditingId(m.id);
    setForm({
      name: m.name,
      kind: m.kind,
      capacityMl: m.capacityMl,
      minTempC: m.minTempC,
      maxTempC: m.maxTempC,
    });
    setErrorMessage(null);
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setForm(emptyForm);
    setErrorMessage(null);
  };

  const handleDelete = async (id: number) => {
    const res = await fetch(`/api/machines/${id}`, { method: "DELETE" });
    if (!res.ok) {
      const text = await res.text();
      setErrorMessage(text || "삭제 중 오류가 발생했습니다.");
      return;
    }
    fetchMachines();
  };

  // ---- 식자재 재고 관리 ----

  const handleInvChange = (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const { name, value } = e.target;
    setInvForm((prev) => ({
      ...prev,
      [name]:
        name === "itemName" || name === "expiryDate" ? value : Number(value),
    }));
  };

  const handleInvSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setInvErrorMessage(null);

    const isEdit = invEditingId !== null;
    const url = isEdit
      ? `/api/inventoryitems/${invEditingId}`
      : "/api/inventoryitems";
    const method = isEdit ? "PUT" : "POST";
    const body = isEdit ? { id: invEditingId, ...invForm } : invForm;

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const text = await res.text();
      setInvErrorMessage(text || "요청 처리 중 오류가 발생했습니다.");
      return;
    }

    setInvForm(emptyInventoryForm);
    setInvEditingId(null);
    fetchInventoryItems();
  };

  const handleInvEdit = (i: InventoryItem) => {
    setInvEditingId(i.id);
    setInvForm({
      itemName: i.itemName,
      quantity: i.quantity,
      expiryDate: i.expiryDate.slice(0, 10),
      threshold: i.threshold,
    });
    setInvErrorMessage(null);
  };

  const handleInvCancelEdit = () => {
    setInvEditingId(null);
    setInvForm(emptyInventoryForm);
    setInvErrorMessage(null);
  };

  const handleInvDelete = async (id: number) => {
    const res = await fetch(`/api/inventoryitems/${id}`, { method: "DELETE" });
    if (!res.ok) {
      const text = await res.text();
      setInvErrorMessage(text || "삭제 중 오류가 발생했습니다.");
      return;
    }
    fetchInventoryItems();
  };

  const handleConsumeAmountChange = (id: number, value: string) => {
    setConsumeAmounts((prev) => ({ ...prev, [id]: value }));
  };

  const handleConsume = async (id: number) => {
    const amountText = consumeAmounts[id] ?? "";
    const amount = Number(amountText);

    const res = await fetch(`/api/inventoryitems/${id}/consume`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ amount }),
    });

    if (!res.ok) {
      const text = await res.text();
      setConsumeErrorByItem((prev) => ({
        ...prev,
        [id]: text || "사용 처리 중 오류가 발생했습니다.",
      }));
      return;
    }

    setConsumeErrorByItem((prev) => {
      const next = { ...prev };
      delete next[id];
      return next;
    });
    setConsumeAmounts((prev) => ({ ...prev, [id]: "" }));
    fetchInventoryItems();
  };

  const isExpired = (expiryDate: string) => expiryDate.slice(0, 10) < todayIso();
  const isLowStock = (item: InventoryItem) => item.quantity < item.threshold;

  return (
    <div style={{ maxWidth: 720, margin: "0 auto", padding: 24 }}>
      <h1>기계 관리</h1>

      <h2>{editingId !== null ? "기계 수정" : "기계 등록"}</h2>
      <form onSubmit={handleSubmit} style={{ marginBottom: 24 }}>
        <div>
          <label>이름: </label>
          <input name="name" value={form.name} onChange={handleChange} />
        </div>
        <div>
          <label>종류: </label>
          <input name="kind" value={form.kind} onChange={handleChange} />
        </div>
        <div>
          <label>용량(ml): </label>
          <input
            name="capacityMl"
            type="number"
            value={form.capacityMl}
            onChange={handleChange}
          />
        </div> 
        <div>
          <label>최저온도(°C): </label>
          <input
            name="minTempC"
            type="number"
            value={form.minTempC}
            onChange={handleChange}
          />
        </div>
        <div>
          <label>최고온도(°C): </label>
          <input
            name="maxTempC"
            type="number"
            value={form.maxTempC}
            onChange={handleChange}
          />
        </div>

        {errorMessage && (
          <p style={{ color: "red" }}>{errorMessage}</p>
        )}

        <button type="submit">
          {editingId !== null ? "수정" : "등록"}
        </button>
        {editingId !== null && (
          <button type="button" onClick={handleCancelEdit}>
            취소
          </button>
        )}
      </form>

      <h2>목록</h2>
      <table border={1} cellPadding={6} style={{ borderCollapse: "collapse" }}>
        <thead>
          <tr>
            <th>이름</th>
            <th>종류</th>
            <th>용량(ml)</th>
            <th>최저온도</th>
            <th>최고온도</th>
            <th>작업</th>
          </tr>
        </thead>
        <tbody>
          {machines.map((m) => (
            <tr key={m.id}>
              <td>{m.name}</td>
              <td>{m.kind}</td>
              <td>{m.capacityMl}</td>
              <td>{m.minTempC}</td>
              <td>{m.maxTempC}</td>
              <td>
                <button onClick={() => handleEdit(m)}>수정</button>
                <button onClick={() => handleDelete(m.id)}>삭제</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <hr style={{ margin: "40px 0" }} />

      <h1>식자재 재고 관리</h1>

      <h2>{invEditingId !== null ? "재고 수정" : "재고 등록"}</h2>
      <form onSubmit={handleInvSubmit} style={{ marginBottom: 24 }}>
        <div>
          <label>재료명: </label>
          <input
            name="itemName"
            value={invForm.itemName}
            onChange={handleInvChange}
          />
        </div>
        <div>
          <label>수량: </label>
          <input
            name="quantity"
            type="number"
            value={invForm.quantity}
            onChange={handleInvChange}
          />
        </div>
        <div>
          <label>유통기한: </label>
          <input
            name="expiryDate"
            type="date"
            value={invForm.expiryDate}
            onChange={handleInvChange}
          />
        </div>
        <div>
          <label>최소 재고 임계치: </label>
          <input
            name="threshold"
            type="number"
            value={invForm.threshold}
            onChange={handleInvChange}
          />
        </div>

        {invErrorMessage && (
          <p style={{ color: "red" }}>{invErrorMessage}</p>
        )}

        <button type="submit">
          {invEditingId !== null ? "수정" : "등록"}
        </button>
        {invEditingId !== null && (
          <button type="button" onClick={handleInvCancelEdit}>
            취소
          </button>
        )}
      </form>

      <h2>목록</h2>
      <table border={1} cellPadding={6} style={{ borderCollapse: "collapse" }}>
        <thead>
          <tr>
            <th>재료명</th>
            <th>수량</th>
            <th>유통기한</th>
            <th>최소 임계치</th>
            <th>상태</th>
            <th>사용(차감)</th>
            <th>작업</th>
          </tr>
        </thead>
        <tbody>
          {inventoryItems.map((i) => (
            <tr key={i.id}>
              <td>{i.itemName}</td>
              <td>{i.quantity}</td>
              <td>{i.expiryDate.slice(0, 10)}</td>
              <td>{i.threshold}</td>
              <td>
                {isExpired(i.expiryDate) && (
                  <span style={{ color: "red" }}>유통기한 지남 </span>
                )}
                {isLowStock(i) && (
                  <span style={{ color: "orange" }}>재고 부족</span>
                )}
              </td>
              <td>
                <input
                  type="number"
                  style={{ width: 60 }}
                  value={consumeAmounts[i.id] ?? ""}
                  onChange={(e) =>
                    handleConsumeAmountChange(i.id, e.target.value)
                  }
                />
                <button onClick={() => handleConsume(i.id)}>사용</button>
                {consumeErrorByItem[i.id] && (
                  <p style={{ color: "red", margin: "4px 0 0" }}>
                    {consumeErrorByItem[i.id]}
                  </p>
                )}
              </td>
              <td>
                <button onClick={() => handleInvEdit(i)}>수정</button>
                <button onClick={() => handleInvDelete(i.id)}>삭제</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default App;
