import React, { useEffect } from 'react';
import VoiceInterface from './components/VoiceInterface';
import { appWindow } from '@tauri-apps/api/window';
import { GripHorizontal } from 'lucide-react';

const App = () => {
  useEffect(() => {
    const initWindow = async () => {
      try {
        if (window.__TAURI__) {
          await appWindow.show();
          await appWindow.setAlwaysOnTop(true);
          await appWindow.center();
        }
      } catch (err) {
        console.error('Tauri initialization error:', err);
      }
    };

    initWindow();
  }, []);

  const handleTitleBarDrag = (e) => {
    if (window.__TAURI__) {
      e.preventDefault();
      appWindow.startDragging();
    }
  };

  return (
    <div className="app-container bg-transparent h-screen w-screen relative">
      <div className="flex items-center justify-center h-full">
        <VoiceInterface />
      </div>
      <div 
        className="absolute bottom-0 left-0 right-0 h-4 draggable-region flex items-center justify-center cursor-move"
        onPointerDown={handleTitleBarDrag}
      >
        <GripHorizontal 
          size={16} 
          className="text-gray-400 opacity-30 hover:opacity-50 transition-opacity"
          strokeWidth={1.5}
        />
      </div>
    </div>
  );
};

export default App;