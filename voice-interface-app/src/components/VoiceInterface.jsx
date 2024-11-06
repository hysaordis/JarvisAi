import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import signalRService, { ServiceStatus, AudioState } from '../services/SignalRService';
import { appWindow } from '@tauri-apps/api/window';

const VoiceInterface = () => {
  const [isListening, setIsListening] = useState(false);
  const [audioLevel, setAudioLevel] = useState(0);
  const [serviceState, setServiceState] = useState(AudioState.IDLE);
  const canvasRef = useRef(null);
  const containerRef = useRef(null);
  const animationFrameRef = useRef();
  const audioLevelHistory = useRef([]);
  const MAX_HISTORY = 5;
  const SMOOTHING_FACTOR = 0.3;

  // Memoize state colors
  const stateColors = useMemo(() => ({
    [AudioState.ERROR]: {
      primary: '#FF3B30',
      glow: 'rgba(255, 59, 48, 0.3)'
    },
    [AudioState.LISTENING]: {
      primary: '#007AFF',
      glow: 'rgba(0, 122, 255, 0.3)'
    },
    [AudioState.PLAYING]: {
      primary: '#32D74B',
      glow: 'rgba(50, 215, 75, 0.3)'
    },
    [AudioState.PROCESSING]: {
      primary: '#FF9500',
      glow: 'rgba(255, 149, 0, 0.3)'
    },
    [AudioState.EXECUTING_FUNCTION]: {
      primary: '#5856D6',
      glow: 'rgba(88, 86, 214, 0.3)'
    },
    [AudioState.IDLE]: {
      primary: '#8E8E93',
      glow: 'rgba(142, 142, 147, 0.3)'
    },
    [AudioState.LISTENING_LONG_SENTENCE]: {
      primary: '#FFD700',
      glow: 'rgba(255, 215, 0, 0.3)'
    }
  }), []);

  const stateLabels = useMemo(() => ({
    [AudioState.ERROR]: 'Error',
    [AudioState.LISTENING]: 'Listening',
    [AudioState.PLAYING]: 'Speaking',
    [AudioState.PROCESSING]: 'Processing',
    [AudioState.EXECUTING_FUNCTION]: 'Executing',
    [AudioState.IDLE]: 'Idle',
    [AudioState.LISTENING_LONG_SENTENCE]: 'Long Sentence Mode'
  }), []);

  const smoothAudioLevel = useCallback((newLevel) => {
    const MAX_HISTORY = 10;
    const SMOOTHING_FACTOR = 0.3;

    audioLevelHistory.current.push(newLevel);
    if (audioLevelHistory.current.length > MAX_HISTORY) {
      audioLevelHistory.current.shift();
    }

    const smoothedLevel = audioLevelHistory.current.reduce((acc, val, idx) => {
      const weight = Math.pow(SMOOTHING_FACTOR, (MAX_HISTORY - idx));
      return acc + (val * weight);
    }, 0) / audioLevelHistory.current.length;

    return Math.min(Math.max(smoothedLevel, 0), 1);
  }, []);

  const handleServiceStatus = useCallback((status) => {
    switch (status) {
      case ServiceStatus.ERROR:
        setServiceState(AudioState.ERROR);
        setIsListening(false);
        break;
      case ServiceStatus.CONNECTED:
        if (!isListening) setServiceState(AudioState.IDLE);
        break;
      case ServiceStatus.DISCONNECTED:
        setServiceState(AudioState.IDLE);
        setIsListening(false);
        break;
      default:
        break;
    }
  }, [isListening]);

  useEffect(() => {
    signalRService.start();
    
    const handleAudioEvent = (event) => {
      if (event.type === 'error') {
        setServiceState(AudioState.ERROR);
      } else if (event.type === 'input') {
        const normalizedLevel = smoothAudioLevel(event.level || 0);
        setAudioLevel(normalizedLevel);
      } else if (event.type === 'state') {
        setServiceState(event.state);
      }
    };

    signalRService.onAudioEvent(handleAudioEvent);
    signalRService.addStatusListener(handleServiceStatus);

    return () => {
      signalRService.removeStatusListener(handleServiceStatus);
      signalRService.connection?.off("AudioEvent");
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [handleServiceStatus, smoothAudioLevel]);

  const drawFunctions = {
    drawGlowCircle: (ctx, centerX, centerY, size, alpha, color) => {
      // Enhanced outer glow
      ctx.shadowBlur = 15;
      ctx.shadowColor = color;
      ctx.beginPath();
      ctx.arc(centerX, centerY, size, 0, Math.PI * 2);
      ctx.fillStyle = color;
      ctx.globalAlpha = alpha * 0.7; // Reduced opacity for softer glow
      ctx.fill();
      ctx.globalAlpha = 1;
    },

    drawMainCircle: (ctx, centerX, centerY, size, alpha, color) => {
      // Enhanced gradient with more color stops
      const gradient = ctx.createRadialGradient(
        centerX, centerY, 0,
        centerX, centerY, size
      );

      // Inner glow effect
      gradient.addColorStop(0, `${color}${Math.floor(alpha * 255).toString(16).padStart(2, '0')}`);
      gradient.addColorStop(0.4, `${color}${Math.floor(alpha * 0.9 * 255).toString(16).padStart(2, '0')}`);
      gradient.addColorStop(0.7, `${color}${Math.floor(alpha * 0.5 * 255).toString(16).padStart(2, '0')}`);
      gradient.addColorStop(0.9, `${color}${Math.floor(alpha * 0.3 * 255).toString(16).padStart(2, '0')}`);
      gradient.addColorStop(1, 'transparent');

      ctx.beginPath();
      ctx.arc(centerX, centerY, size, 0, Math.PI * 2);
      ctx.fillStyle = gradient;
      ctx.fill();

      // Add inner highlight
      const innerGradient = ctx.createRadialGradient(
        centerX - size * 0.3, centerY - size * 0.3, size * 0.1,
        centerX, centerY, size
      );
      innerGradient.addColorStop(0, `rgba(255, 255, 255, ${alpha * 0.4})`);
      innerGradient.addColorStop(1, 'transparent');
      
      ctx.beginPath();
      ctx.arc(centerX, centerY, size, 0, Math.PI * 2);
      ctx.fillStyle = innerGradient;
      ctx.fill();
    }
  };

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return; // Aggiungi questo controllo

    const ctx = canvas.getContext('2d', { alpha: true });
    let startTime = Date.now();
    const baseSize = 35; // Base size for the circle
    const idlePulse = Math.sin(Date.now() / 1000) * 2;
    const { primary, glow } = stateColors[serviceState];

    const handleResize = () => {
      if (!containerRef.current) return;
      
      const dpr = window.devicePixelRatio || 1;
      // Fixed dimensions
      const containerWidth = 200;
      const containerHeight = 200;
      
      canvas.width = containerWidth * dpr;
      canvas.height = containerHeight * dpr;
      canvas.style.width = `${containerWidth}px`;
      canvas.style.height = `${containerHeight}px`;
      
      ctx.scale(dpr, dpr);
      ctx.clearRect(0, 0, canvas.width, canvas.height);
    };

    window.addEventListener('resize', handleResize);
    handleResize();

    const animate = () => {
      const time = (Date.now() - startTime) / 1000;
      const { width, height } = canvas;
      
      ctx.clearRect(0, 0, width, height);
      ctx.globalCompositeOperation = 'source-over';
      
      const centerX = width / (2 * (window.devicePixelRatio || 1));
      const centerY = height / (2 * (window.devicePixelRatio || 1));
      let currentSize = baseSize + idlePulse;

      ctx.shadowBlur = 15; // Increased shadow blur
      ctx.shadowColor = glow;

      if (serviceState === AudioState.LISTENING || 
          serviceState === AudioState.LISTENING_LONG_SENTENCE) {
        currentSize += Math.sin(time * 3.5) * 6; // Smoother animation
        drawFunctions.drawGlowCircle(ctx, centerX, centerY, currentSize * 1.5, 0.35, glow);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize * 1.2, 0.45, primary);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize, 0.7, primary);
      } else if (serviceState === AudioState.PLAYING) {
        currentSize += Math.sin(time * 4) * 5.6;
        drawFunctions.drawGlowCircle(ctx, centerX, centerY, currentSize * 1.4, 0.3, glow);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize * 1.1, 0.4, primary);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize, 0.6, primary);
      } else if (serviceState === AudioState.ERROR) {
        currentSize += Math.sin(time * 8) * 3.5;
        drawFunctions.drawGlowCircle(ctx, centerX, centerY, currentSize * 1.3, 0.4, glow);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize * 1.1, 0.5, primary);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize, 0.7, primary);
      } else if (serviceState === AudioState.PROCESSING) {
        currentSize += Math.sin(time * 4) * 7;
        drawFunctions.drawGlowCircle(ctx, centerX, centerY, currentSize * 1.3, 0.25, glow);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize * 1.1, 0.35, primary);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize, 0.5, primary);
      } else {
        drawFunctions.drawGlowCircle(ctx, centerX, centerY, currentSize * 1.3, 0.15, glow);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize * 1.1, 0.25, primary);
        drawFunctions.drawMainCircle(ctx, centerX, centerY, currentSize, 0.4, primary);
      }

      animationFrameRef.current = requestAnimationFrame(animate);
    };

    animate();
    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [audioLevel, serviceState, stateColors]);

  const toggleListening = async () => {
    try {
      if (serviceState === AudioState.IDLE) {
        // Start listening if currently idle
        await signalRService.startListening();
        setIsListening(true);
      } else {
        // Stop listening if in any other state
        await signalRService.stopListening();
        setIsListening(false);
      }
    } catch (err) {
      console.error('Toggle Listening Error:', err);
      setServiceState(AudioState.ERROR);
      setIsListening(false);
    }
  };

  const startDrag = useCallback(async (e) => {
    if (e.target === canvasRef.current) return;
    try {
      await appWindow.startDragging();
    } catch (err) {
      console.error('Drag error:', err);
    }
  }, []);

  return (
    <div
      ref={containerRef}
      onPointerDown={startDrag}
      className="flex flex-col items-center justify-center draggable-region"
      style={{ 
        background: 'transparent',
        height: '200px', // Increased height
        width: '200px',  // Increased width
        cursor: 'move',
        touchAction: 'none',
        position: 'relative'
      }}
    >
      <div className="window-drag-handle" onPointerDown={startDrag} />
      
      {/* Long Sentence Mode Message */}
      {serviceState === AudioState.LISTENING_LONG_SENTENCE && (
        <div 
          className="absolute top-0 left-1/2 transform -translate-x-1/2 px-3 py-1 rounded-full 
                    backdrop-blur-md transition-all duration-300 text-xs font-medium"
          style={{
            backgroundColor: `${stateColors[AudioState.LISTENING_LONG_SENTENCE].primary}22`,
            color: stateColors[AudioState.LISTENING_LONG_SENTENCE].primary,
            border: `1px solid ${stateColors[AudioState.LISTENING_LONG_SENTENCE].primary}44`,
            textShadow: `0 0 10px ${stateColors[AudioState.LISTENING_LONG_SENTENCE].glow}`,
            whiteSpace: 'nowrap'
          }}
        >
          Say "stop" to end
        </div>
      )}

      {/* Main Canvas */}
      <canvas
        ref={canvasRef}
        className="non-draggable-region cursor-pointer transition-transform duration-200 hover:scale-105 active:scale-95"
        onClick={toggleListening}
        style={{ 
          width: '250px',  // Increased width
          height: '250px', // Increased height
          background: 'transparent',
          pointerEvents: 'auto'
        }}
      />
      
      {/* Status Label */}
      <div 
        className="absolute bottom-0 left-1/2 transform -translate-x-1/2 px-3 py-1 rounded-full 
                   backdrop-blur-md transition-all duration-300 text-sm font-medium"
        style={{
          backgroundColor: `${stateColors[serviceState].primary}22`,
          color: stateColors[serviceState].primary,
          border: `1px solid ${stateColors[serviceState].primary}44`,
          textShadow: `0 0 10px ${stateColors[serviceState].glow}`,
          opacity: serviceState === AudioState.IDLE ? 0.7 : 1,
          transform: `translate(-50%, ${serviceState === AudioState.IDLE ? '0' : '0'})`,
          whiteSpace: 'nowrap'
        }}
      >
        {stateLabels[serviceState]}
      </div>
    </div>
  );
};

export default VoiceInterface;