import React, { useEffect, useState } from 'react';
import VoiceInterface from './components/VoiceInterface';
import LogConsole from './components/LogConsole';
import ChatInterface from './components/ChatInterface';
import { appWindow } from '@tauri-apps/api/window';
import { GripHorizontal, Terminal, MessageSquare, Volume2, VolumeX, Minus, Maximize2, Minimize2 } from 'lucide-react';
import signalRService from './services/SignalRService';
import { invoke } from '@tauri-apps/api/tauri';

const App = () => {
  const [showConsole, setShowConsole] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showChat, setShowChat] = useState(false);  // Aggiungi questo stato

  useEffect(() => {
    const initWindow = async () => {
      try {
        if (window.__TAURI__) {
          await appWindow.show();
          await appWindow.setAlwaysOnTop(true);
          await appWindow.center();
        }

        // Get initial mute state
        const muteState = await signalRService.getVoiceMuteState();
        setIsMuted(muteState);
      } catch (err) {
        console.error('Initialization error:', err);
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

  // Update toggle function to use SignalR
  const toggleMute = async () => {
    try {
      const newMuteState = !isMuted;
      await signalRService.setVoiceMute(newMuteState);
      setIsMuted(newMuteState);
    } catch (err) {
      console.error('Failed to toggle mute state:', err);
    }
  };

  const handleMinimizeToTray = async () => {
    if (window.__TAURI__) {
      await invoke('minimize_to_tray');
    }
  };

  const toggleFullscreen = async () => {
    if (window.__TAURI__) {
      const isCurrentlyFullscreen = await appWindow.isFullscreen();
      await appWindow.setFullscreen(!isCurrentlyFullscreen);
      setIsFullscreen(!isCurrentlyFullscreen);
    }
  };

  return (
    <div className="app-container bg-transparent h-screen w-screen relative flex flex-col">
      {/* Header Bar - Now entirely draggable */}
      <div 
        className="h-10 bg-black/10 backdrop-blur-sm flex items-center justify-between px-4 draggable-region"
        onPointerDown={handleTitleBarDrag}
      >
        {/* Left side - App title/logo */}
        <div className="text-gray-400 text-sm font-medium select-none">
          Alita AI
        </div>

        {/* Right side - Controls - Non-draggable */}
        <div className="flex items-center space-x-2" onPointerDown={(e) => e.stopPropagation()}>
          <button
            onClick={toggleMute}
            className="flex items-center space-x-1 px-3 py-1.5 rounded-md bg-black/20 
                     hover:bg-black/30 transition-all duration-200 text-gray-400 hover:text-white"
          >
            {isMuted ? <VolumeX size={16} /> : <Volume2 size={16} />}
            <span className="text-xs">{isMuted ? 'Muted' : 'Active'}</span>
          </button>

          {/* Chat button */}
          <button
            onClick={() => setShowChat(!showChat)}
            className="flex items-center space-x-1 px-3 py-1.5 rounded-md bg-black/20 
                     hover:bg-black/30 transition-all duration-200 text-gray-400 hover:text-white"
          >
            <MessageSquare size={16} />
            <span className="text-xs">Chat</span>
          </button>

          <button
            onClick={() => setShowConsole(!showConsole)}
            className="flex items-center space-x-1 px-3 py-1.5 rounded-md bg-black/20 
                     hover:bg-black/30 transition-all duration-200 text-gray-400 hover:text-white"
          >
            <Terminal size={16} />
            <span className="text-xs">Console</span>
          </button>

          <div className="h-4 w-px bg-gray-500/20"></div>

          <button
            onClick={toggleFullscreen}
            className="p-1.5 rounded-md bg-black/20 
                     hover:bg-black/30 transition-all duration-200 text-gray-400 hover:text-white"
            aria-label="Toggle fullscreen"
          >
            {isFullscreen ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
          </button>

          <button
            onClick={handleMinimizeToTray}
            className="p-1.5 rounded-md bg-black/20 
                     hover:bg-black/30 transition-all duration-200 text-gray-400 hover:text-white"
            aria-label="Minimize"
          >
            <Minus size={16} strokeWidth={1.5} />
          </button>
        </div>
      </div>

      {/* Main Content Area - Rimuovi il pannello chat */}
      <div className="flex-1">
        <div className="h-full p-6">
          <div className="h-full flex items-center justify-center 
                        bg-black/5 backdrop-blur-sm rounded-lg
                        border border-white/5">
            <VoiceInterface />
          </div>
        </div>
      </div>

      {/* Floating Components */}
      <LogConsole isVisible={showConsole} onClose={() => setShowConsole(false)} />
      <ChatInterface isVisible={showChat} onClose={() => setShowChat(false)} />
    </div>
  );
};

export default App;