import * as signalR from "@microsoft/signalr";

class SignalRService {
  private connection: signalR.HubConnection;
  private readonly hubUrl: string = "http://localhost:5066/flighthub";

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect() // Built-in retry policy for transient failures
      .build();
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

  public onReceiveFlightData(callback: (flightId: string, lat: number, lng: number, heading: number, altitude: number, speed: number, targetLat: number, targetLng: number) => void): void {
    this.connection.on("ReceiveFlightData", callback);
  }

  public onReceiveChatMessage(callback: (user: string, text: string) => void): void {
    this.connection.on("ReceiveChatMessage", callback);
  }

  public async sendChatMessage(user: string, message: string): Promise<void> {
    await this.connection.invoke("ProcessChatMessage", user, message);
  }
}

export const signalRService = new SignalRService();
