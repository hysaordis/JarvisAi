import React, { useState, useRef, useEffect } from 'react';
import signalRService from '../services/SignalRService';

const LogConsole = ({ isVisible, onClose }) => {
  const [logs, setLogs] = useState([]);
  const consoleRef = useRef(null);
  const maxLogs = 100;

  useEffect(() => {
    const handleLogEvent = (logEvent) => {
      setLogs(prevLogs => {
        const newLogs = [...prevLogs, {
          id: Date.now(),
          ...logEvent,
          timestamp: new Date(logEvent.timestamp).toLocaleTimeString()
        }].slice(-maxLogs);
        return newLogs;
      });
    };

    signalRService.connection.on("LogEvent", handleLogEvent);

    return () => {
      signalRService.connection.off("LogEvent", handleLogEvent);
    };
  }, []);

  useEffect(() => {
    if (consoleRef.current) {
      consoleRef.current.scrollTop = consoleRef.current.scrollHeight;
    }
  }, [logs]);

  useEffect(() => {
    const handleKeyPress = (e) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyPress);
    return () => {
      window.removeEventListener('keydown', handleKeyPress);
    };
  }, [onClose]);

  const getLogLevelStyle = (logLevel) => {
    switch (logLevel.toLowerCase()) {
      case 'error':
        return 'text-red-500';
      case 'warning':
        return 'text-yellow-500';
      case 'information':
        return 'text-blue-500';
      case 'debug':
        return 'text-gray-400';
      default:
        return 'text-white';
    }
  };

  if (!isVisible) return null;

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-lg rounded-lg border border-gray-700 
                    shadow-lg transition-all duration-300 ease-in-out cursor-default"
         style={{ zIndex: 1000 }}>
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2 border-b border-gray-700 cursor-move rounded-t-lg">
        <div className="flex items-center space-x-2">
          <button
            onClick={onClose}
            className="w-4 h-4 bg-red-500 rounded-full hover:bg-red-600 transition-colors"
          />
        </div>
        <h3 className="text-white text-sm font-medium absolute left-1/2 transform -translate-x-1/2">Terminal</h3>
      </div>

      {/* Log Container */}
      <div
        ref={consoleRef}
        className="h-[calc(100%-40px)] overflow-y-auto font-mono text-xs p-4 space-y-1 rounded-b-lg"
        style={{
          scrollbarWidth: 'thin',
          scrollbarColor: 'rgba(255,255,255,0.2) transparent'
        }}
      >
        {logs.map(log => (
          <div key={log.id} className="flex flex-col space-y-1">
            <span className="text-gray-500">{log.timestamp}</span>
            <span className={getLogLevelStyle(log.logLevel)}>{log.message}</span>
          </div>
        ))}
      </div>
    </div>
  );
};

export default LogConsole;
