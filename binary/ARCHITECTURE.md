# Screen Capture App - Architecture Documentation

## Overview

The Screen Capture App is a binary trading automation system designed to interact with Pocket Option trading platform through browser automation. The system uses computer vision, OCR (Optical Character Recognition), and human-like mouse/keyboard interactions to execute trading operations.

## System Architecture

### High-Level Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Client GUI    │    │   Server        │    │   Browser       │
│   (Tkinter)     │◄──►│   (Socket)      │◄──►│   Manager       │
│                 │    │                 │    │   (Automation)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
        │                       │                       │
        │                       │                       │
        ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   User Input    │    │   Command       │    │   Pocket Option │
│   & Display     │    │   Processing    │    │   Web Platform  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Core Components

### 1. Client (`client_standalone_for_debug.py`)

**Purpose**: Provides a graphical user interface for user interaction and command sending.

**Key Features**:
- Tkinter-based GUI with log display and command input
- Socket client for communication with the server
- Real-time logging and status display
- Test functionality for buy commands
- Standalone debug mode for testing

**Responsibilities**:
- Establish TCP connection to server (127.0.0.1:65300)
- Send JSON-formatted commands to server
- Display server responses and system logs
- Provide user-friendly interface for trading operations
- Handle connection errors and reconnection

**Dependencies**:
- `socket` - Network communication
- `tkinter` - GUI framework
- `json` - Command serialization
- `threading` - Asynchronous communication

### 2. Server (`server_pocket_option.py`)

**Purpose**: Acts as a command processor and coordinator between client and browser automation.

**Key Features**:
- Socket server listening on port 65300
- Command parsing and validation
- Integration with browser automation service
- Response handling and logging
- Error handling and recovery

**Responsibilities**:
- Accept client connections
- Parse incoming JSON commands
- Route commands to appropriate automation functions
- Return execution results to client
- Manage browser automation lifecycle
- Handle client disconnections gracefully

### 3. Browser Manager (`pocket_option_browser_manager.py`)

**Purpose**: Core automation engine that interacts with the Pocket Option web platform.

**Key Features**:
- Human-like mouse and keyboard interactions
- Window management and screenshot capture
- Trading operation execution (buy/sell)
- Enhanced window focus management
- Symbol selection and parameter setting

**Core Classes**:

#### BrowserManagerService
- **Window Management**: Finds and controls browser windows with enhanced focus
- **Screenshot Capture**: Takes window screenshots for debugging
- **Trading Operations**: Executes buy/sell orders with specified parameters
- **Symbol Management**: Handles symbol selection by index
- **Risk/Duration Setting**: Configures trading parameters

**Key Methods**:
- `activate_window()` - Sets window focus using win32gui.SetForegroundWindow
- `open_symbol_tab()` - Opens symbol selection by index
- `set_risk()` - Configures risk amount with keyboard input
- `set_duration()` - Sets trading duration with area clicks or keyboard input
- `buy()` / `sell()` - Execute complete trading operations
- `get_symbol_area()` - Calculate symbol button coordinates

**Dependencies**:
- `win32gui/win32con/win32ui` - Windows API for window management
- `helpers` - Centralized input control classes

### 4. Helpers (`helpers.py`)

**Purpose**: Centralized input control and utility functions.

**Key Features**:
- Human-like mouse and keyboard simulation
- Window detection and management
- Screenshot capture utilities
- Input timing and randomization

**Core Classes**:

#### HumanMouse
- **Human-like Movement**: Simulates natural mouse movements using Bezier curves
- **Random Variations**: Adds realistic timing and path variations
- **Area Operations**: Click and move within specified areas
- **Methods**:
  - `move_to(x, y, duration)` - Smooth movement to coordinates
  - `click(x, y, button, duration)` - Click with movement
  - `click_area(area)` - Click random point within area
  - `move_mouse_to_area(area)` - Move to random point within area

#### HumanKeyboard
- **Human-like Typing**: Simulates natural keyboard input timing
- **Key Operations**: Press, release, and special key handling
- **Methods**:
  - `send_keys(text)` - Type text with natural timing
  - `press_key(key)` - Press and release specific key
  - `press_backspace(count)` - Press backspace multiple times
  - `keyboard_press_key_sleep()` - Add random delays

**Utility Functions**:
- `find_browser_window()` - Locate Pocket Option browser window
- `set_window_size()` - Configure window dimensions
- `screenshot_window()` - Capture window content

**Dependencies**:
- `pynput` - Mouse and keyboard control
- `win32gui/win32con/win32ui` - Windows API
- `PIL` - Image processing

### 5. Debug Utilities (`debug_screenshot.py`)

**Purpose**: Diagnostic tool for troubleshooting screenshot and window detection issues.

**Features**:
- Window enumeration and identification
- Screenshot testing with detailed logging
- Error diagnosis and reporting
- Manual window detection testing

## Data Flow

### 1. Command Flow
```
User Input → Client GUI → Socket → Server → Browser Manager → Pocket Option Platform
```

### 2. Response Flow
```
Pocket Option Platform → Browser Manager → Server → Socket → Client GUI → User Display
```

### 3. Screenshot Processing Flow
```
Browser Window → Screenshot Capture → Image Storage
```

### 4. Trading Operation Flow
```
Command → Window Focus → Symbol Selection → Risk Setting → Duration Setting → Buy/Sell Click
```

## Key Technologies

### Image Processing
- **PIL/Pillow**: Image handling and format conversion

### Automation
- **pynput**: Human-like mouse and keyboard control
- **pyautogui**: Additional automation capabilities
- **Windows API**: Direct window management and control
- **humanmouse**: Realistic mouse movement simulation

### Communication
- **Socket Programming**: TCP-based client-server communication
- **JSON**: Command and response serialization

### GUI
- **Tkinter**: Cross-platform GUI framework
- **Threading**: Asynchronous operations

## Configuration

### Dependencies
- Python 3.x
- Required Python packages:
  - pynput
  - pyautogui
  - pywin32
  - Pillow
  - humanmouse

### Environment Setup
1. Install Python dependencies: `pip install -r requirements.txt`
2. Ensure Pocket Option browser window is open
3. Run server first, then client
4. Verify window detection and focus

## Security Considerations

### Current Implementation
- Local-only communication (127.0.0.1)
- No authentication mechanism
- Direct window manipulation
- Human-like input simulation for anti-detection

### Recommended Improvements
- Implement user authentication
- Add command validation and sanitization
- Encrypt sensitive data transmission
- Add rate limiting for trading operations
- Implement session management

## Error Handling

### Screenshot Failures
- Window not found detection
- Screenshot capture retry logic
- Graceful fallback mechanisms

### Network Issues
- Connection timeout handling
- Automatic reconnection attempts
- Graceful degradation

### Trading Operation Failures
- Symbol validation
- Risk parameter verification
- Execution confirmation
- Window focus recovery

### Window Management Issues
- Window not found recovery
- Focus setting failures
- Window state detection

## Performance Considerations

### Optimization Strategies
- Screenshot caching for debugging
- Minimal mouse movements for faster execution
- Efficient window focus management
- Optimized input timing

### Resource Usage
- Memory management for image processing
- Network bandwidth for real-time communication
- CPU usage for human-like input simulation

## Monitoring and Logging

### Logging Levels
- **INFO**: Normal operation events
- **WARNING**: Potential issues
- **ERROR**: Operation failures
- **DEBUG**: Detailed diagnostic information

### Key Metrics
- Screenshot capture success rate
- Trading operation success rate
- Response times
- Window focus success rate
- Input simulation accuracy

## Recent Refactoring Changes

### Code Organization Improvements
- **Centralized Input Control**: All mouse and keyboard operations moved to `helpers.py`
- **Separation of Concerns**: Browser manager focuses on business logic, helpers handle input
- **Enhanced Maintainability**: Input behavior changes only require updates in one location
- **Improved Reusability**: Input classes can be used across different components

### Window Focus Enhancements
- **Enhanced Focus Management**: Improved window activation with win32gui.SetForegroundWindow
- **Automatic Focus**: Window focus set during initialization
- **Focus Before Operations**: All trading operations ensure window focus first
- **Error Handling**: Graceful handling of focus-related errors

### Input Simulation Improvements
- **HumanMouse Class**: Centralized mouse control with area operations
- **HumanKeyboard Class**: Centralized keyboard control with natural timing
- **Consistent Timing**: Standardized delays and randomization across all input operations

## Future Enhancements

### Planned Features
- Multi-platform support (Linux, macOS)
- Advanced trading strategies
- Risk management automation
- Performance analytics dashboard
- Mobile client application
- Real-time market data integration

### Technical Improvements
- Advanced pattern recognition
- Cloud-based configuration management
- Enhanced error recovery mechanisms
- Performance optimization
- Advanced anti-detection measures

## Troubleshooting

### Common Issues
1. **Window Not Found**: Ensure Pocket Option browser is open and visible
2. **Connection Refused**: Check if server is running on correct port
3. **Permission Errors**: Run with appropriate Windows permissions
4. **Focus Issues**: Verify window is not minimized and is accessible
5. **Input Failures**: Check if other applications are capturing input

### Debug Tools
- `debug_screenshot.py` for screenshot testing
- `client_standalone_for_debug.py` for standalone testing
- Detailed logging in all components
- Manual window detection utilities

### Debug Commands
- Test window detection and focus
- Verify screenshot capture
- Test individual trading operations
- Monitor input simulation accuracy

## Conclusion

The Screen Capture App provides a robust foundation for automated binary trading operations with enhanced input simulation and window management. The recent refactoring has improved code organization, maintainability, and reliability while maintaining the core functionality for real-time trading scenarios. The modular architecture allows for easy maintenance and future enhancements. 