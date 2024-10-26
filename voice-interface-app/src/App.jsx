import { useEffect } from 'react';
import VoiceInterface from './components/VoiceInterface';
import { appWindow } from "@tauri-apps/api/window";

function App() {
  useEffect(() => {
    // Mantieni sempre in primo piano
    appWindow.setAlwaysOnTop(true);

    const handleClose = async () => {
      await appWindow.hide();
    };

    window.addEventListener('beforeunload', handleClose);
    return () => window.removeEventListener('beforeunload', handleClose);
  }, []);

  return (
    <div className="h-screen bg-transparent flex items-center justify-center">
      <VoiceInterface />
    </div>
  );
}

export default App;