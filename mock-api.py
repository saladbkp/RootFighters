import asyncio
import json
import websockets

async def handler(websocket):
    print(" Unity 前端已连接！")
    try:
        while True:
            # 在终端等待你的指令输入
            cmd = input("请输入测试指令 (1:A队解出Pwn, 2:B队解出Web, 3:A队答错): ")
            
            payload = {}
            if cmd == "1":
                payload = {"event_type": "SOLVE", "team_name": "Team_A", "category": "Pwn", "score": 100}
            elif cmd == "2":
                payload = {"event_type": "SOLVE", "team_name": "Team_B", "category": "Web", "score": 100}
            elif cmd == "3":
                payload = {"event_type": "WRONG", "team_name": "Team_A", "category": "None", "score": 0}
            else:
                print("未知指令")
                continue

            # 通过 WebSocket 发送给 Unity
            await websocket.send(json.dumps(payload))
            print(f"已向大屏广播数据: {payload}")
            
    except websockets.ConnectionClosed:
        print("Unity 前端断开连接")

async def main():
    async with websockets.serve(handler, "localhost", 8080):
        print("Mock 后端已启动，等待 localhost:8080 连接...")
        await asyncio.Future() # 保持运行

if __name__ == "__main__":
    asyncio.run(main())