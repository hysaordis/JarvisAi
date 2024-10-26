import React, { useState, useEffect } from 'react';
import { appWindow } from '@tauri-apps/api/window';

const VoiceInterface = () => {
  const [isListening, setIsListening] = useState(false);
  const [audioLevel, setAudioLevel] = useState(0);
  const [wavePhase, setWavePhase] = useState(0);

  useEffect(() => {
    let interval;
    if (isListening) {
      interval = setInterval(() => {
        setAudioLevel(0.3 + Math.random() * 0.7);
        setWavePhase(prev => (prev + 0.1) % (2 * Math.PI));
      }, 50);
    } else {
      setAudioLevel(0);
    }
    return () => clearInterval(interval);
  }, [isListening]);

  const toggleListening = () => {
    setIsListening(!isListening);
  };

  // Genera le onde concentriche
  const generateWaves = () => {
    const numberOfWaves = 8;
    return Array.from({ length: numberOfWaves }).map((_, index) => {
      const phase = wavePhase + (index * Math.PI / 4);
      const baseScale = 1 + (index * 0.12);
      const waveAmplitude = isListening ? (audioLevel * 0.15) : 0.05;
      const scale = baseScale + Math.sin(phase) * waveAmplitude;
      
      return (
        <div
          key={index}
          className="absolute inset-0 rounded-full transition-all duration-200"
          style={{
            transform: `scale(${scale})`,
            border: '1px solid rgba(56, 189, 248, 0.2)',
            background: 'radial-gradient(circle, rgba(56, 189, 248, 0.1) 0%, rgba(56, 189, 248, 0) 100%)',
            opacity: 1 - (index / numberOfWaves),
            transition: 'transform 0.2s ease-out'
          }}
        />
      );
    });
  };

  return (
    <div 
      className="relative"
      data-tauri-drag-region  // Aggiunge la possibilità di trascinare
    >
        <div 
          className="relative w-20 h-20 cursor-pointer"
          onClick={(e) => {
            e.stopPropagation(); // Previene il drag quando si clicca il pulsante
            toggleListening();
          }}
        >
        {/* Onde concentriche animate */}
        {generateWaves()}

        {/* Nucleo centrale */}
        <div 
          className={`absolute inset-0 flex items-center justify-center rounded-full 
            transition-all duration-500 overflow-hidden
            ${isListening ? 'scale-95' : 'scale-100'}`}
          style={{
            background: 'radial-gradient(circle, rgba(56, 189, 248, 1) 0%, rgba(14, 165, 233, 1) 100%)',
            boxShadow: isListening 
              ? '0 0 20px rgba(56, 189, 248, 0.5)' 
              : '0 0 15px rgba(14, 165, 233, 0.3)'
          }}>
          
          {/* Indicatore di stato centrale con effetto onda */}
          <div 
            className="w-12 h-12 rounded-full relative overflow-hidden"
            style={{
              background: 'rgba(186, 230, 253, 0.2)',
              transform: `scale(${0.6 + (audioLevel * 0.4)})`,
            }}
          >
            {/* Onde animate all'interno del cerchio centrale */}
            <div
              className="absolute inset-0"
              style={{
                background: `
                  radial-gradient(circle at 50% ${50 - (audioLevel * 20)}%, 
                    rgba(186, 230, 253, 0.3) 0%,
                    rgba(186, 230, 253, 0.1) 50%,
                    transparent 100%)
                `
              }}
            />
          </div>
        </div>

        {/* Effetto onda in primo piano */}
        <div 
          className="absolute inset-0 rounded-full"
          style={{
            background: `
              radial-gradient(circle at 50% ${50 + Math.sin(wavePhase) * 10}%, 
                transparent 0%,
                rgba(56, 189, 248, 0.1) 70%,
                rgba(14, 165, 233, 0.2) 100%)
            `,
            opacity: isListening ? 1 : 0,
            transition: 'opacity 0.5s ease-out'
          }}
        />
      </div>

      {/* Definizione dell'animazione per le onde */}
      <style jsx>{`
        @keyframes wave {
          0% { transform: translateY(0%); }
          50% { transform: translateY(-20%); }
          100% { transform: translateY(0%); }
        }
      `}</style>
    </div>
  );
};

export default VoiceInterface;