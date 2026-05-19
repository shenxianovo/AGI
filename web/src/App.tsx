import { BrowserRouter, Routes, Route } from "react-router-dom";
import StatusDashboard from "./components/StatusDashboard";
import OperatorView from "./components/OperatorView";
import "./index.css";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<StatusDashboard />} />
        <Route path="/operator" element={<OperatorView />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
