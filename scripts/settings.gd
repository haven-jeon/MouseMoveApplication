extends Control

var monitors = []
var selected_monitor = 0
var cursor_speed = 5

# Called when the node enters the scene tree for the first time.
func _ready():
    # Connect button signals
    $Panel/VBoxContainer/ButtonsContainer/SaveButton.connect("pressed", Callable(self, "_on_save_button_pressed"))
    $Panel/VBoxContainer/ButtonsContainer/CancelButton.connect("pressed", Callable(self, "_on_cancel_button_pressed"))
    $Panel/VBoxContainer/MonitorSelector.connect("item_selected", Callable(self, "_on_monitor_selected"))
    $Panel/VBoxContainer/SpeedSlider.connect("value_changed", Callable(self, "_on_speed_changed"))
    
    # Set up monitor selector
    setup_monitor_selector()
    
    # Load settings
    load_settings()

# Set up the monitor selection dropdown
func setup_monitor_selector():
    var monitor_selector = $Panel/VBoxContainer/MonitorSelector
    monitor_selector.clear()
    
    # Get display count
    var display_count = DisplayServer.get_screen_count()
    
    # Populate monitors list with display information
    for i in range(display_count):
        var monitor_name = "Monitor " + str(i+1)
        var ssize = DisplayServer.screen_get_size(i)
        monitor_selector.add_item(monitor_name + " (" + str(ssize.x) + "x" + str(ssize.y) + ")")
        monitors.append(i)

# Load settings from file
func load_settings():
    var config = ConfigFile.new()
    var err = config.load("user://settings.cfg")
    
    if err == OK:
        # Load monitor index
        selected_monitor = config.get_value("mouse", "monitor_index", 0)
        if selected_monitor < $Panel/VBoxContainer/MonitorSelector.get_item_count():
            $Panel/VBoxContainer/MonitorSelector.select(selected_monitor)
        
        # Load cursor speed
        cursor_speed = config.get_value("mouse", "speed", 5)
        $Panel/VBoxContainer/SpeedSlider.value = cursor_speed
    else:
        # Default settings if file doesn't exist
        $Panel/VBoxContainer/MonitorSelector.select(0)
        $Panel/VBoxContainer/SpeedSlider.value = 5

# Save settings to file
func save_settings():
    var config = ConfigFile.new()
    
    # Save monitor index and speed
    config.set_value("mouse", "monitor_index", selected_monitor)
    config.set_value("mouse", "speed", cursor_speed)
    
    # Write to file
    config.save("user://settings.cfg")

# Handle monitor selection
func _on_monitor_selected(index):
    selected_monitor = index

# Handle speed change
func _on_speed_changed(value):
    cursor_speed = value

# Save settings and return to main menu
func _on_save_button_pressed():
    save_settings()
    get_tree().change_scene_to_file("res://scenes/main.tscn")

# Return to main menu without saving
func _on_cancel_button_pressed():
    get_tree().change_scene_to_file("res://scenes/main.tscn")
