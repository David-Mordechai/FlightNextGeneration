import * as signalR from "@microsoft/signalr";

class SignalRService {
  private connection: signalR.HubConnection;
  private readonly hubUrl: string = "http://localhost:5135/flighthub";

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
          if (retryContext.elapsedMilliseconds < 60000) {
            // If we've been reconnecting for less than 60 seconds, retry every 2s
            return 2000;
          } else {
            // Otherwise, retry every 10s
            return 10000;
          }
        }
      })
      .build();

    this.connection.onclose(async () => {
      console.warn("SignalR Connection Closed. Re-starting...");
      await this.startConnection();
    });

    this.connection.onreconnecting((error) => {
      console.warn(`SignalR Reconnecting: ${error}`);
    });

    this.connection.onreconnected((connectionId) => {
      console.log(`SignalR Reconnected. ID: ${connectionId}`);
    });
  }

  public async startConnection(): Promise<void> {
    const start = async () => {
      try {
        await this.connection.start();
        console.log("SignalR Connected.");
      } catch (err) {
        console.error("SignalR Connection Error: ", err);
        // Infinite retry for initial connection
        setTimeout(start, 5000);
      }
    };

    await start();
  }

  public async stopConnection(): Promise<void> {
    try {
      await this.connection.stop();
      console.log("SignalR Disconnected.");
    } catch (err) {
      console.error("SignalR Stop Error: ", err);
    }
  }

  public onReceiveFlightData(callback: (data: any) => void): void {
    this.connection.on("ReceiveFlightData", callback);
  }

  public onReceiveChatMessage(callback: (user: string, text: string, duration?: number) => void): void {
    this.connection.on("ReceiveChatMessage", callback);
  }

  public onEntityUpdate(callback: (update: { entityType: string; changeType: string; data: any }) => void): void {
    this.connection.on("EntityUpdateReceived", callback);
  }

  public on(eventName: string, callback: (...args: any[]) => void): void {
    this.connection.on(eventName, callback);
  }

  public async sendChatMessage(user: string, message: string): Promise<void> {
    await this.connection.invoke("ProcessChatMessage", user, message);
  }

  public async checkAiStatus(): Promise<boolean> {
    if (this.connection.state !== signalR.HubConnectionState.Connected) return false;
    return await this.connection.invoke("CheckAiStatus");
  }
}

export const signalRService = new SignalRService();
