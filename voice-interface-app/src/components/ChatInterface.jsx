import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Send, X } from 'lucide-react';
import signalRService from '../services/SignalRService';

const shortenUrl = (url) => {
  try {
    const urlObj = new URL(url);
    const domain = urlObj.hostname;
    const path = urlObj.pathname;
    
    if (url.length > 30) {
      return `${domain}${path.length > 15 ? path.substring(0, 15) + '...' : path}`;
    }
    return url;
  } catch {
    return url;
  }
};

const convertLinksToJSX = (text) => {
  const urlRegex = /(https?:\/\/[^\s]+)/g;
  const parts = text.split(urlRegex);
  
  return parts.map((part, index) => {
    if (part.match(urlRegex)) {
      return (
        <a
          key={index}
          href={part}
          target="_blank"
          rel="noopener noreferrer"
          className="text-blue-400 hover:text-blue-300 underline break-all"
          onClick={(e) => e.stopPropagation()}
          title={part} // Mostra l'URL completo al passaggio del mouse
        >
          {shortenUrl(part)}
        </a>
      );
    }
    return part;
  });
};

const ChatInterface = ({ isVisible, onClose }) => {  // Aggiungi le props
  const [messages, setMessages] = useState([]);
  const [inputText, setInputText] = useState('');
  const [lastScrollPosition, setLastScrollPosition] = useState(0);
  const messagesEndRef = useRef(null);
  const scrollContainerRef = useRef(null);

  // Salva la posizione di scroll quando la chat viene chiusa
  const handleClose = useCallback(() => {
    if (scrollContainerRef.current) {
      setLastScrollPosition(scrollContainerRef.current.scrollTop);
    }
    onClose();
  }, [onClose]);

  // Ripristina la posizione di scroll quando la chat viene aperta
  useEffect(() => {
    if (isVisible && scrollContainerRef.current) {
      scrollContainerRef.current.scrollTop = lastScrollPosition;
    }
  }, [isVisible, lastScrollPosition]);

  // Modifica l'effetto esistente dello scroll automatico per rispettare la posizione salvata
  useEffect(() => {
    if (scrollContainerRef.current && messages.length > 0) {
      const shouldAutoScroll = 
        scrollContainerRef.current.scrollHeight - 
        scrollContainerRef.current.scrollTop - 
        scrollContainerRef.current.clientHeight < 100;

      if (shouldAutoScroll) {
        const { scrollHeight, clientHeight } = scrollContainerRef.current;
        scrollContainerRef.current.scrollTo({
          top: scrollHeight - clientHeight,
          behavior: 'smooth'
        });
      }
    }
  }, [messages]);

  useEffect(() => {
    const handleChatMessage = (message) => {
      setMessages(prev => [...prev, message]);
    };

    signalRService.addChatListener(handleChatMessage);
    return () => signalRService.removeChatListener(handleChatMessage);
  }, []);

  const handleSendMessage = async (e) => {
    e.preventDefault();
    if (!inputText.trim()) return;

    const userMessage = {
      id: Date.now(),
      text: inputText,
      sender: 'user',
      timestamp: new Date().toLocaleTimeString()
    };

    setMessages(prev => [...prev, userMessage]);
    setInputText('');

    try {
      await signalRService.sendChatMessage(inputText);
    } catch (error) {
      setMessages(prev => [...prev, {
        id: Date.now(),
        text: "Sorry, I couldn't send your message. Please try again.",
        sender: 'system',
        timestamp: new Date().toLocaleTimeString()
      }]);
    }
  };

  if (!isVisible) return null;

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-lg rounded-lg border border-gray-700 
                    shadow-lg transition-all duration-300 ease-in-out flex flex-col"
         style={{ zIndex: 1000 }}>
      {/* macOS style header */}
      <div className="flex items-center h-8 px-3 bg-gray-900/50 border-b border-gray-700/50 rounded-t-lg">
        <div className="flex items-center space-x-2">
          <button
            onClick={handleClose}
            className="w-4 h-4 bg-red-500 rounded-full hover:bg-red-600 
                     flex items-center justify-center group transition-colors"
          >
            <X size={10} className="text-red-900 opacity-0 group-hover:opacity-100" />
          </button>
        </div>
        <div className="flex-1 flex justify-center">
          <span className="text-sm text-gray-400">Chat with Alita</span>
        </div>
      </div>

      {/* Messages Container */}
      <div 
        ref={scrollContainerRef}
        className="flex-1 overflow-y-auto scroll-smooth px-4 py-3 min-h-0"
        style={{
          scrollbarWidth: 'thin',
          scrollbarColor: 'rgba(255,255,255,0.2) transparent'
        }}
      >
        <div className="space-y-4">
          {messages.map(message => (
            <div
              key={message.id}
              className={`flex ${message.sender === 'user' ? 'justify-end' : 'justify-start'}
                         animate-in fade-in slide-in-from-bottom-2 duration-300`}
            >
              <div 
                className={`max-w-[80%] rounded-2xl px-4 py-3 shadow-lg
                           ${message.sender === 'user'
                             ? 'bg-blue-500/30 text-blue-50 rounded-br-sm'
                             : 'bg-gray-700/40 text-gray-100 rounded-bl-sm'
                           } backdrop-blur-sm border border-white/10`}
              >
                <div className="text-sm leading-relaxed break-words">
                  {convertLinksToJSX(message.text)}
                </div>
                <div className="text-[10px] mt-1.5 opacity-60">{message.timestamp}</div>
              </div>
            </div>
          ))}
        </div>
        <div ref={messagesEndRef} className="h-4" />
      </div>

      {/* Input Area */}
      <div className="border-t border-gray-700/50 bg-black/30 backdrop-blur-md p-3">
        <form onSubmit={handleSendMessage}>
          <div className="flex items-center space-x-2">
            <input
              type="text"
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              className="flex-1 bg-gray-900/50 text-white rounded-xl px-4 py-2.5
                       border border-gray-700/50 backdrop-blur-sm
                       focus:outline-none focus:ring-2 focus:ring-blue-500/40
                       placeholder-gray-400 text-sm"
              placeholder="Message Alita..."
            />
            <button
              type="submit"
              className="p-2.5 rounded-xl bg-blue-500/20 text-blue-400 
                       hover:bg-blue-500/30 transition-colors
                       border border-blue-500/30 hover:border-blue-500/50"
            >
              <Send size={16} />
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ChatInterface;
