import * as signalR from '@microsoft/signalr';

// Constants for states
export const ServiceStatus = {
  DISCONNECTED: 'disconnected',
  INITIALIZING: 'initializing',
  CONNECTED: 'connected',
  ERROR: 'error',
  RECONNECTING: 'reconnecting'
};

export const AudioState = {
  IDLE: 'idle',
  LISTENING: 'listening',
  PLAYING: 'playing',
  PROCESSING: 'processing',
  EXECUTING_FUNCTION: 'executingfunction',
  ERROR: 'error'
};

class SignalRService {
  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:7456/alitahub', {
        withCredentials: true,
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, null])
      .configureLogging(signalR.LogLevel.Debug)
      .build();

    this.serviceStatus = ServiceStatus.DISCONNECTED;
    this.audioState = AudioState.IDLE;
    this.heartbeatInterval = null;
    this.statusListeners = new Set();
    this.audioStateListeners = new Set();
    this.isConnected = false;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = 5;
    this.baseReconnectDelay = 2000;
  }

  addStatusListener(callback) {
    this.statusListeners.add(callback);
  }

  removeStatusListener(callback) {
    this.statusListeners.delete(callback);
  }

  addAudioStateListener(callback) {
    this.audioStateListeners.add(callback);
  }

  removeAudioStateListener(callback) {
    this.audioStateListeners.delete(callback);
  }

  notifyStatusListeners(status) {
    this.serviceStatus = status;
    this.statusListeners.forEach(listener => listener(status));
  }

  notifyAudioStateListeners(state) {
    this.audioState = state;
    this.audioStateListeners.forEach(listener => listener(state));
  }

  async start() {
    try {
      if (this.connection.state === signalR.HubConnectionState.Disconnected) {
        this.notifyStatusListeners(ServiceStatus.INITIALIZING);
        await this.tryConnect();
      }
    } catch (err) {
      console.error('SignalR Connection Error:', err);
      this.handleConnectionError();
    }
  }

  async tryConnect() {
    try {
      await this.connection.start();
      console.log('SignalR Connected.');
      this.isConnected = true;
      this.reconnectAttempts = 0;
      
      this.setupConnectionEvents();
      this.startHeartbeat();
      
      this.connection.on("ServiceStatusChanged", (data) => {
        if (!data) {
          console.warn('Received invalid ServiceStatusChanged data');
          this.notifyStatusListeners(ServiceStatus.ERROR);
          return;
        }
        const newStatus = this.mapToServiceStatus(data.status);
        this.notifyStatusListeners(newStatus);
      });

      await this.startup();
      
    } catch (err) {
      console.error(`Connection attempt failed (${this.reconnectAttempts + 1}/${this.maxReconnectAttempts}):`, err);
      
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.reconnectAttempts++;
        const delay = this.calculateReconnectDelay();
        this.notifyStatusListeners(ServiceStatus.RECONNECTING);
        
        console.log(`Retrying connection in ${delay}ms...`);
        setTimeout(() => this.tryConnect(), delay);
      } else {
        console.error('Max reconnection attempts reached');
        this.notifyStatusListeners(ServiceStatus.ERROR);
        // Reset attempts counter after some time to allow future reconnection attempts
        setTimeout(() => {
          this.reconnectAttempts = 0;
        }, 60000);
      }
    }
  }

  calculateReconnectDelay() {
    // Exponential backoff with jitter
    const exponentialDelay = this.baseReconnectDelay * Math.pow(2, this.reconnectAttempts);
    const jitter = Math.random() * 1000;
    return Math.min(exponentialDelay + jitter, 30000); // Cap at 30 seconds
  }

  startHeartbeat() {
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }

    this.heartbeatInterval = setInterval(async () => {
      if (!this.isConnected) return;

      try {
        await this.connection.invoke('HeartbeatAsync')
          .catch(err => {
            console.warn('Heartbeat failed:', err);
            this.handleConnectionError();
          });
      } catch (err) {
        console.warn('Heartbeat error:', err);
        this.handleConnectionError();
      }
    }, 30000);
  }

  async startup() {
    try {
      await this.connection.invoke('StartupAsync');
    } catch (err) {
      console.error('Startup Error:', err);
      throw err;
    }
  }

  async startListening() {
    try {
      let retryCount = 0;
      const maxRetries = 3;
      
      while (retryCount < maxRetries) {
        try {
          const status = await this.connection.invoke('GetServiceStatus');
          
          if (status === ServiceStatus.ERROR || status === ServiceStatus.DISCONNECTED) {
            await this.connection.invoke('InitializeServiceAsync');
            await new Promise((resolve, reject) => {
              const timeout = setTimeout(() => reject(new Error('Service initialization timeout')), 10000);
              const statusHandler = (newStatus) => {
                if (newStatus === ServiceStatus.CONNECTED) {
                  clearTimeout(timeout);
                  this.removeStatusListener(statusHandler);
                  resolve();
                } else if (newStatus === ServiceStatus.ERROR) {
                  clearTimeout(timeout);
                  this.removeStatusListener(statusHandler);
                  reject(new Error('Service initialization failed'));
                }
              };
              this.addStatusListener(statusHandler);
            });
          }

          await this.connection.invoke('StartListeningAsync');
          this.notifyAudioStateListeners(AudioState.LISTENING);
          break;
          
        } catch (err) {
          retryCount++;
          if (retryCount === maxRetries) {
            throw new Error('Failed to start listening after multiple attempts');
          }
          await new Promise(resolve => setTimeout(resolve, 2000));
        }
      }
    } catch (err) {
      console.error('Start Listening Error:', err);
      this.notifyAudioStateListeners(AudioState.ERROR);
      throw err;
    }
  }

  async stopListening() {
    try {
      await this.connection.invoke('StopListeningAsync');
      this.notifyAudioStateListeners(AudioState.IDLE);
    } catch (err) {
      console.error('Stop Listening Error:', err);
      this.notifyAudioStateListeners(AudioState.ERROR);
      throw err;
    }
  }

  setupConnectionEvents() {
    this.connection.onclose(() => {
      console.log('SignalR Connection closed');
      this.isConnected = false;
      this.notifyStatusListeners(ServiceStatus.DISCONNECTED);
      this.notifyAudioStateListeners(AudioState.IDLE);
      
      // Try to reconnect if not already at max attempts
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.tryConnect();
      }
    });

    this.connection.onreconnecting(() => {
      console.log('SignalR Reconnecting...');
      this.isConnected = false;
      this.notifyStatusListeners(ServiceStatus.RECONNECTING);
    });

    this.connection.onreconnected(async () => {
      console.log('SignalR Reconnected');
      this.isConnected = true;
      this.reconnectAttempts = 0;
      this.notifyStatusListeners(ServiceStatus.CONNECTED);
      
      // Reinitialize service state after reconnection
      try {
        await this.startup();
      } catch (err) {
        console.error('Error reinitializing after reconnection:', err);
        this.handleConnectionError();
      }
    });
  }

  handleConnectionError() {
    this.isConnected = false;
    this.notifyStatusListeners(ServiceStatus.ERROR);
    this.notifyAudioStateListeners(AudioState.ERROR);
    
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
      this.heartbeatInterval = null;
    }

    // Only attempt reconnection if not at max attempts
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      const delay = this.calculateReconnectDelay();
      setTimeout(() => this.tryConnect(), delay);
    }
  }

  onAudioEvent(callback) {
    this.connection?.off("AudioEvent");
    
    this.connection.on("AudioEvent", (eventData) => {
      if (!eventData || !eventData.type) {
        console.warn('Received invalid audio event data');
        return;
      }

      try {
        if (eventData.error) {
          console.warn('Audio Event Error:', eventData.error);
          this.notifyAudioStateListeners(AudioState.ERROR);
          callback({ type: 'error', error: eventData.error });
          return;
        }

        switch (eventData.type) {
          case "audio.input.level":
            callback({ 
              type: 'input', 
              level: eventData.level,
              timestamp: eventData.timestamp
            });
            break;
          case "audio.state":
            const newState = this.mapBackendToAudioState(eventData.state);
            this.notifyAudioStateListeners(newState);
            callback({ 
              type: 'state', 
              state: newState,
              timestamp: eventData.timestamp
            });
            break;
          default:
            console.warn('Unknown audio event type:', eventData.type);
        }
      } catch (err) {
        console.error('Error processing audio event:', err);
        this.notifyAudioStateListeners(AudioState.ERROR);
        callback({ type: 'error', error: 'Event processing failed' });
      }
    });
  }

  mapToServiceStatus(status) {
    if (!status) {
      console.warn('Received undefined or null status');
      return ServiceStatus.ERROR;
    }

    const normalizedStatus = status.toLowerCase();
    switch (normalizedStatus) {
      case 'disconnected': return ServiceStatus.DISCONNECTED;
      case 'initializing': return ServiceStatus.INITIALIZING;
      case 'connected': return ServiceStatus.CONNECTED;
      case 'error': return ServiceStatus.ERROR;
      case 'reconnecting': return ServiceStatus.RECONNECTING;
      default: 
        console.warn(`Unknown status received: ${status}`);
        return ServiceStatus.ERROR;
    }
  }

  mapBackendToAudioState(backendState) {
    if (!backendState) return AudioState.ERROR;
    
    const stateMap = {
      'idle': AudioState.IDLE,
      'listening': AudioState.LISTENING,
      'playing': AudioState.PLAYING,
      'processing': AudioState.PROCESSING,
      'executingfunction': AudioState.EXECUTING_FUNCTION,
      'error': AudioState.ERROR
    };

    return stateMap[backendState.toLowerCase()] || AudioState.ERROR;
  }

  dispose() {
    try {
      if (this.heartbeatInterval) {
        clearInterval(this.heartbeatInterval);
        this.heartbeatInterval = null;
      }

      if (this.connection) {
        this.connection.off("AudioEvent");
        this.connection.off("ServiceStatusChanged");
        this.connection.stop();
      }

      this.isConnected = false;
      this.statusListeners.clear();
      this.audioStateListeners.clear();
    } catch (err) {
      console.error('Error during SignalR service disposal:', err);
    }
  }
}

const signalRService = new SignalRService();
export default signalRService;