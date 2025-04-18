extends Control

var mouse_movement_active = false
var cursor_controller = null
var settings = {
    "monitor_index": 0,
    "speed": 5
}

var pow = 2 

@onready var mouse_mover: Node = %MouseMover


# Called when the node enters the scene tree for the first time.
func _ready():
    # Connect button signals
    $VBoxContainer/StartButton.connect("pressed", Callable(self, "_on_start_button_pressed"))
    $VBoxContainer/SettingsButton.connect("pressed", Callable(self, "_on_settings_button_pressed"))
    $VBoxContainer/ExitButton.connect("pressed", Callable(self, "_on_exit_button_pressed"))
    
    # Load settings
    load_settings()
    
    # Initialize cursor controller with mouse_mover node reference
    cursor_controller = CursorController.new(mouse_mover)
    add_child(cursor_controller)

# Load saved settings
func load_settings():
    var config = ConfigFile.new()
    var err = config.load("user://settings.cfg")
    
    if err == OK:
        settings.monitor_index = config.get_value("mouse", "monitor_index", 0)
        settings.speed = config.get_value("mouse", "speed", 5)
    else:
        # Default settings if file doesn't exist
        settings.monitor_index = 0
        settings.speed = 5

# Toggle mouse movement on/off
func _on_start_button_pressed():
    mouse_movement_active = !mouse_movement_active
    
    if mouse_movement_active:
        cursor_controller.start_movement(settings.monitor_index, settings.speed * pow)
        $VBoxContainer/StartButton.text = "Stop Mouse Movement"
    else:
        cursor_controller.stop_movement()
        $VBoxContainer/StartButton.text = "Start Mouse Movement"

# Open the settings scene
func _on_settings_button_pressed():
    get_tree().change_scene_to_file("res://scenes/settings.tscn")

# Exit the application
func _on_exit_button_pressed():
    get_tree().quit()

# Cursor controller class that manages the automated mouse movement
class CursorController:
    extends Node
    
    var movement_active = false
    var monitor_index = 0
    var speed = 5
    var direction = Vector2(1, 1)
    var target_position = Vector2.ZERO
    var screen_size = Vector2.ZERO
    var timer = null
    var rng = RandomNumberGenerator.new()
    var mouse_mover = null
    
    # Constructor with mouse_mover parameter
    func _init(mover_node):
        mouse_mover = mover_node
    
    func _ready():
        rng.randomize()
        timer = Timer.new()
        timer.wait_time = 0.05
        timer.connect("timeout", Callable(self, "_on_timer_timeout"))
        add_child(timer)
    
    # Start automated mouse movement
    func start_movement(monitor_idx, move_speed):
        monitor_index = monitor_idx
        speed = move_speed
        movement_active = true
        
        # Get screen size of selected monitor
        var displays = DisplayServer.get_screen_count()
        if monitor_index >= displays:
            monitor_index = 0
            
        # 첫 시작 시, C# 스크립트가 모니터 정보를 갱신하도록
        mouse_mover.RefreshMonitorInfo()
        
        # 중앙 지향적 초기 목표점 설정
        target_position = mouse_mover.GenerateCenterBiasedTargetInMonitor(monitor_index)
        
        print_debug('초기 목표 위치: ' + str(target_position))
        
        # Start movement timer
        timer.start()
    
    # Stop automated movement
    func stop_movement():
        movement_active = false
        timer.stop()
    
    # Called when the timer times out
    func _on_timer_timeout():
        if movement_active and mouse_mover:
            # C#에서 현재 마우스 위치 가져오기
            var current_pos = mouse_mover.GetMousePosition()
            
            # 자연스러운 마우스 이동 - C# 함수 호출
            mouse_mover.MoveMouseHumanLike(current_pos, Vector2i(target_position), speed, monitor_index)
            
            # 현재 위치가 목표에 가까우면 새로운 목표점 설정
            if current_pos.distance_to(Vector2i(target_position)) < 10:
                # 중앙 지향적인 새 목표점 생성 (C# 메서드 사용)
                target_position = mouse_mover.GenerateCenterBiasedTargetInMonitor(monitor_index)
                
                # 사람처럼 보이도록 가끔 타이머 간격 랜덤화 (멈춤 효과)
                if rng.randf() < 0.1: # 10% 확률로 잠시 멈춤
                    timer.stop()
                    await get_tree().create_timer(rng.randf_range(0.1, 0.5)).timeout
                    timer.start()
