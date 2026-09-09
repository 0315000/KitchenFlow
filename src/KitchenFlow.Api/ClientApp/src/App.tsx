import { useEffect, useState } from 'react'

type Machine = {
  id: number
  name: string
}

function App() {
  const [machines, setMachines] = useState<Machine[]>([])

  useEffect(() => {
    fetch('/api/machines')
      .then((res) => res.json())
      .then((data) => setMachines(data))
  }, [])

  return (
    <ul>
      {machines.map((machine) => (
        <li key={machine.id}>{machine.name}</li>
      ))}
    </ul>
  )
}

export default App
