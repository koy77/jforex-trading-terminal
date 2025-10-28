<?php
// Read log file and get all lines
$logFile = 'app.log';
$logLines = [];
$foundTags = [];

if (file_exists($logFile)) {
    $logContent = file_get_contents($logFile);
    $logLines = explode("\n", $logContent);
    $logLines = array_filter($logLines, function($line) {
        return !empty(trim($line));
    });
    
    // Extract unique tags from log lines
    foreach ($logLines as $line) {
        // Look for pattern [LEVEL][TAG] or [LEVEL] [TAG]
        if (preg_match('/\[([^\]]+)\]\[([^\]]+)\]/', $line, $matches)) {
            $tag = $matches[2];
            if (!in_array($tag, $foundTags)) {
                $foundTags[] = $tag;
            }
        }
    }
    
    // Sort tags alphabetically
    sort($foundTags);
    
    // Reverse lines to show newest first
    $logLines = array_reverse($logLines);
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Log Viewer</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            background-color: #1e1e1e;
            color: #d4d4d4;
            height: 100vh;
            overflow: hidden;
        }
        
        .controls {
            background-color: #2d2d30;
            padding: 10px 20px;
            display: flex;
            gap: 20px;
            align-items: center;
            border-bottom: 1px solid #555;
            flex-shrink: 0;
        }
        
        .control-group {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        
        label {
            color: #9cdcfe;
            font-size: 12px;
            font-weight: bold;
            white-space: nowrap;
        }
        
        select, input {
            background-color: #3c3c3c;
            color: #d4d4d4;
            border: 1px solid #555;
            padding: 6px 8px;
            border-radius: 4px;
            font-family: inherit;
            font-size: 12px;
        }
        
        select:focus, input:focus {
            outline: none;
            border-color: #007acc;
        }
        
        .stats {
            display: flex;
            gap: 20px;
            margin-left: auto;
        }
        
        .stat-item {
            display: flex;
            align-items: center;
            gap: 5px;
        }
        
        .stat-number {
            font-size: 14px;
            font-weight: bold;
            color: #4ec9b0;
        }
        
        .stat-label {
            font-size: 11px;
            color: #9cdcfe;
        }
        
        .log-content {
            height: calc(100vh - 60px);
            overflow-y: auto;
            padding: 0;
        }
        
        .log-entry {
            padding: 4px 15px;
            border-bottom: 1px solid #333;
            font-size: 14px;
            line-height: 1.3;
            word-wrap: break-word;
            white-space: pre-wrap;
        }
        
        .log-entry:hover {
            background-color: #2a2d2e;
        }
        
        .log-entry:last-child {
            border-bottom: none;
        }
        
        .no-logs {
            text-align: center;
            padding: 40px;
            color: #808080;
            font-style: italic;
        }
    </style>
</head>
<body>
    <div class="controls">
        <div class="control-group">
            <label for="level-filter">Level:</label>
            <select id="level-filter">
                <option value="">All</option>
                <option value="INFO">INFO</option>
                <option value="DEBUG">DEBUG</option>
                <option value="WARNING">WARNING</option>
                <option value="ERROR">ERROR</option>
                <option value="SOCKET">SOCKET</option>
            </select>
        </div>
        
        <div class="control-group">
            <label for="tag-filter">Tag:</label>
            <select id="tag-filter">
                <option value="">All Tags</option>
                <?php foreach ($foundTags as $tag): ?>
                <option value="<?php echo htmlspecialchars($tag); ?>"><?php echo htmlspecialchars($tag); ?></option>
                <?php endforeach; ?>
            </select>
        </div>
        
        <div class="control-group">
            <label for="message-filter">Message:</label>
            <input type="text" id="message-filter" placeholder="Search...">
        </div>
        
        <div class="stats">
            <div class="stat-item">
                <span class="stat-number" id="total-logs">0</span>
                <span class="stat-label">Total</span>
            </div>
            <div class="stat-item">
                <span class="stat-number" id="filtered-logs">0</span>
                <span class="stat-label">Filtered</span>
            </div>
        </div>
    </div>
    
    <div class="log-content" id="log-content">
        <div class="no-logs">Loading logs...</div>
    </div>

    <script>
        // Embedded log lines from PHP
        const allLogLines = <?php echo json_encode($logLines); ?>;
        
        function updateStats(totalCount, filteredCount) {
            document.getElementById('total-logs').textContent = totalCount;
            document.getElementById('filtered-logs').textContent = filteredCount;
        }
        
        function filterLogs() {
            const levelFilter = document.getElementById('level-filter').value;
            const tagFilter = document.getElementById('tag-filter').value;
            const messageFilter = document.getElementById('message-filter').value.toLowerCase();
            
            let filteredLines = allLogLines.filter(line => {
                // Level filter - check if line contains the level
                if (levelFilter && !line.includes('[' + levelFilter + ']')) {
                    return false;
                }
                
                // Tag filter - check if line contains the selected tag
                if (tagFilter && !line.includes('[' + tagFilter + ']')) {
                    return false;
                }
                
                // Message filter - check if line contains the message text
                if (messageFilter && !line.toLowerCase().includes(messageFilter)) {
                    return false;
                }
                
                return true;
            });
            
            updateStats(allLogLines.length, filteredLines.length);
            displayLogs(filteredLines);
        }
        
        function displayLogs(lines) {
            const logContent = document.getElementById('log-content');
            
            if (lines.length === 0) {
                logContent.innerHTML = '<div class="no-logs">No logs match the current filters.</div>';
                return;
            }
            
            const html = lines.map(line => {
                // Escape HTML and add some basic styling
                const escapedLine = line.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                return `<div class="log-entry">${escapedLine}</div>`;
            }).join('');
            
            logContent.innerHTML = html;
        }
        
        // Event listeners
        document.getElementById('level-filter').addEventListener('change', filterLogs);
        document.getElementById('tag-filter').addEventListener('change', filterLogs);
        document.getElementById('message-filter').addEventListener('input', filterLogs);
        
        // Initial load - show all logs
        filterLogs();
    </script>
</body>
</html>