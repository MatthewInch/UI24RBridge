import { useState } from 'react';
import { MemoryRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';


const startLoggingMIDIInput = (midiAccess: WebMidi.MIDIAccess) => {
  const onMIDIMessage = (event) => {
    let str = `MIDI message received at timestamp ${event.timeStamp}[${event.data.length} bytes]: `;
    // eslint-disable-next-line no-restricted-syntax
    for (const character of event.data) {
      str += `0x${character.toString(16)} `;
    }
    console.log(str);
  };
  midiAccess.inputs.forEach((entry) => {
    entry.onmidimessage = onMIDIMessage;
  });
};

const getMidiInputsList = async () => {
  const midiAccess = await navigator.requestMIDIAccess();
  const inputList: Array<string> = [];
  midiAccess.inputs.forEach((entry) => {
    if (entry.name) {
      inputList.push(entry.name);
    }
  });
  startLoggingMIDIInput(midiAccess);
  return inputList;
};

const Hello = () => {
  const [midiInputsName, setMidiInputsName] = useState<Array<string>>([]);

  const fetchData = async () => {
    const input = await getMidiInputsList();
    setMidiInputsName(input);
  };

  return (
    <div>
      <h1>Midi test</h1>
      <button type="button" title="Refresh" onClick={fetchData}>
        Refresh
      </button>
      {midiInputsName.length > 0 && (
        <ul>
          {midiInputsName.map((value) => {
            return <li>{value}</li>;
          })}
        </ul>
      )}
    </div>
  );
};

export default function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Hello />} />
      </Routes>
    </Router>
  );
}
