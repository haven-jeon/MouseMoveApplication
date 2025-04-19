using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class MouseMover: Node
{
	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int X, int Y);
	
	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);
	
	[DllImport("user32.dll")]
	private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
	
	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
	
	private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
	
	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}
	
	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
	
	[StructLayout(LayoutKind.Sequential)]
	public struct MONITORINFO
	{
		public uint Size;
		public RECT Monitor;
		public RECT WorkArea;
		public uint Flags;
	}
	
	private List<Rect2I> windowsMonitorRects = new List<Rect2I>();
	
	public override void _Ready()
	{
		RefreshMonitorInfo();
	}
	
	public void RefreshMonitorInfo()
	{
		windowsMonitorRects.Clear();
		EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorCallback, IntPtr.Zero);
		
		// 디버그 정보 출력
		GD.Print($"총 {windowsMonitorRects.Count}개 모니터 탐지됨 (Windows API)");
		for (int i = 0; i < windowsMonitorRects.Count; i++)
		{
			GD.Print($"Windows 모니터 {i}: 위치({windowsMonitorRects[i].Position.X},{windowsMonitorRects[i].Position.Y}), " +
					 $"크기({windowsMonitorRects[i].Size.X}x{windowsMonitorRects[i].Size.Y})");
		}
		
		// Godot API 비교 - 수정된 메서드 이름 사용
		int screenCount = DisplayServer.GetScreenCount();
		GD.Print($"총 {screenCount}개 모니터 탐지됨 (Godot API)");
		for (int i = 0; i < screenCount; i++)
		{
			var pos = DisplayServer.ScreenGetPosition(i);
			var size = DisplayServer.ScreenGetSize(i);
			GD.Print($"Godot 모니터 {i}: 위치({pos.X},{pos.Y}), 크기({size.X}x{size.Y})");
		}
	}
	
	private bool MonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
	{
		MONITORINFO monitorInfo = new MONITORINFO();
		monitorInfo.Size = (uint)Marshal.SizeOf(typeof(MONITORINFO));
		GetMonitorInfo(hMonitor, ref monitorInfo);
		
		windowsMonitorRects.Add(new Rect2I(
			monitorInfo.Monitor.Left, 
			monitorInfo.Monitor.Top,
			monitorInfo.Monitor.Right - monitorInfo.Monitor.Left,
			monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top));
		
		return true;
	}
	
	public Vector2I GetMousePosition()
	{
		POINT point;
		GetCursorPos(out point);
		return new Vector2I(point.X, point.Y);
	}

	public void MoveMouseToPosition(Vector2I position)
	{
		SetCursorPos(position.X, position.Y);
	}
	
	// 모니터 인덱스에 맞는 Windows 시스템 모니터 영역 가져오기
	private Rect2I GetMonitorRect(int monitorIndex)
	{
		// 최초 실행 시 또는 모니터 정보가 비어있으면 갱신
		if (windowsMonitorRects.Count == 0)
		{
			RefreshMonitorInfo();
		}
		
		// 유효한 모니터 인덱스인지 확인
		if (monitorIndex >= 0 && monitorIndex < windowsMonitorRects.Count)
		{
			return windowsMonitorRects[monitorIndex];
		}
		
		// 폴백: 기본 모니터 또는 첫 번째 모니터 반환
		return windowsMonitorRects.Count > 0 ? windowsMonitorRects[0] : new Rect2I(0, 0, 1920, 1080);
	}
	
	// 사람처럼 보이도록 부드럽게 마우스 움직임 (더 자연스러운 곡선 이동)
	public void MoveMouseHumanLike(Vector2I startPosition, Vector2I targetPosition, float speed, int monitorIndex = -1)
	{
		// 디버그: 모니터 인덱스 정보 출력 (자세한 디버깅 목적이 아니면 주석 처리 가능)
		// GD.Print($"선택된 모니터: {monitorIndex}");
		
		// 현재 위치에서 목표 위치까지의 벡터 계산
		Vector2 direction = targetPosition - startPosition;
		
		// 속도에 따른 이동 거리 계산 (실제 사람처럼 약간의 변동성 추가)
		float randomFactor = (float)GD.RandRange(0.8, 1.2); // 80%-120% 속도 변동
		float distance = speed * randomFactor;
		
		// 거리가 너무 짧으면 바로 도착
		if (direction.Length() <= distance)
		{
			SetCursorPos(targetPosition.X, targetPosition.Y);
			return;
		}
		
		// 방향 정규화 후 이동 거리 적용
		direction = direction.Normalized() * distance;
		
		// 약간의 무작위 변동 추가 (사람 손 떨림 시뮬레이션) - 더 자연스러운 값으로 조정
		float jitter = (float)GD.RandRange(-0.7, 0.7) * (speed * 0.2f);
		direction.X += jitter;
		direction.Y += jitter;
		
		// 새 위치 계산
		Vector2I newPosition = startPosition + new Vector2I((int)direction.X, (int)direction.Y);

		// 특정 모니터로 제한
		if (monitorIndex >= 0)
		{
			// Windows API로부터 모니터 경계 가져오기
			var monitorRect = GetMonitorRect(monitorIndex);
			
			// 새 위치가 선택한 모니터 영역을 벗어나면 경계 내로 조정
			if (!monitorRect.HasPoint(newPosition))
			{
				// 가장자리보다 약간 안쪽으로 제한 (10픽셀)
				int margin = 10;
				newPosition.X = Mathf.Clamp(newPosition.X, 
											monitorRect.Position.X + margin, 
											monitorRect.Position.X + monitorRect.Size.X - margin);
				newPosition.Y = Mathf.Clamp(newPosition.Y, 
											monitorRect.Position.Y + margin, 
											monitorRect.Position.Y + monitorRect.Size.Y - margin);
			}
		}
		
		// 최종 위치 설정
		SetCursorPos(newPosition.X, newPosition.Y);
	}

	// 모니터 중앙 지향적인 목표점 생성 함수 추가
	public Vector2I GenerateCenterBiasedTargetInMonitor(int monitorIndex)
	{
		var monitorRect = GetMonitorRect(monitorIndex);
		
		// 모니터 중심점
		Vector2I center = new Vector2I(
			monitorRect.Position.X + monitorRect.Size.X / 2,
			monitorRect.Position.Y + monitorRect.Size.Y / 2
		);
		
		// 중앙에 더 많은 확률로 위치하는 타겟 생성
		// 가우시안 분포와 유사하게 중앙 주변에 더 많은 점이 위치하도록 함
		Random rand = new Random();
		double u1 = 1.0 - rand.NextDouble(); // 0에 가까울수록 위험
		double u2 = 1.0 - rand.NextDouble();
		
		// Box-Muller 변환을 사용한 정규 분포
		double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
		
		// 편차 조정 (모니터 크기의 25% 정도로 설정)
		double stdDev = Math.Min(monitorRect.Size.X, monitorRect.Size.Y) * 0.25;
		
		// 중앙으로부터 오프셋 계산
		int xOffset = (int)(randStdNormal * stdDev);
		
		// 두 번째 정규 분포 값 계산
		randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
		int yOffset = (int)(randStdNormal * stdDev);
		
		// 최종 목표점 (모니터 중심에서 오프셋 적용)
		Vector2I target = new Vector2I(
			center.X + xOffset,
			center.Y + yOffset
		);
		
		// 모니터 영역 내로 제한 (여백 추가)
		int margin = 20;
		target.X = Mathf.Clamp(target.X, 
							  monitorRect.Position.X + margin, 
							  monitorRect.Position.X + monitorRect.Size.X - margin);
		target.Y = Mathf.Clamp(target.Y, 
							  monitorRect.Position.Y + margin, 
							  monitorRect.Position.Y + monitorRect.Size.Y - margin);
		
		return target;
	}
}
