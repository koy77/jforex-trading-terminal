<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Log Viewer</title>
    <style>
        body {
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            margin: 0;
            padding: 20px;
            background-color: #1e1e1e;
            color: #d4d4d4;
        }
        
        .container {
            max-width: 1400px;
            margin: 0 auto;
        }
        
        h1 {
            color: #569cd6;
            text-align: center;
            margin-bottom: 30px;
        }
        
        .controls {
            background-color: #2d2d30;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            align-items: center;
        }
        
        .control-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }
        
        label {
            color: #9cdcfe;
            font-size: 12px;
            font-weight: bold;
        }
        
        select, input {
            background-color: #3c3c3c;
            color: #d4d4d4;
            border: 1px solid #555;
            padding: 8px;
            border-radius: 4px;
            font-family: inherit;
        }
        
        select:focus, input:focus {
            outline: none;
            border-color: #007acc;
        }
        
        button {
            background-color: #007acc;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 4px;
            cursor: pointer;
            font-family: inherit;
            font-weight: bold;
        }
        
        button:hover {
            background-color: #005a9e;
        }
        
        .stats {
            background-color: #2d2d30;
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: flex;
            gap: 30px;
            flex-wrap: wrap;
        }
        
        .stat-item {
            display: flex;
            flex-direction: column;
            align-items: center;
        }
        
        .stat-number {
            font-size: 24px;
            font-weight: bold;
            color: #4ec9b0;
        }
        
        .stat-label {
            font-size: 12px;
            color: #9cdcfe;
        }
        
        .log-container {
            background-color: #1e1e1e;
            border: 1px solid #555;
            border-radius: 8px;
            overflow: hidden;
        }
        
        .log-header {
            background-color: #2d2d30;
            padding: 10px 15px;
            border-bottom: 1px solid #555;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        
        .log-content {
            max-height: 600px;
            overflow-y: auto;
            padding: 0;
        }
        
        .log-entry {
            padding: 8px 15px;
            border-bottom: 1px solid #333;
            font-size: 13px;
            line-height: 1.4;
            word-wrap: break-word;
        }
        
        .log-entry:hover {
            background-color: #2a2d2e;
        }
        
        .log-entry:last-child {
            border-bottom: none;
        }
        
        .timestamp {
            color: #608b4e;
            font-weight: bold;
        }
        
        .level {
            font-weight: bold;
            padding: 2px 6px;
            border-radius: 3px;
            margin: 0 5px;
        }
        
        .level.INFO {
            background-color: #4ec9b0;
            color: #1e1e1e;
        }
        
        .level.DEBUG {
            background-color: #9cdcfe;
            color: #1e1e1e;
        }
        
        .level.WARNING {
            background-color: #dcdcaa;
            color: #1e1e1e;
        }
        
        .level.ERROR {
            background-color: #f44747;
            color: white;
        }
        
        .level.SOCKET {
            background-color: #c586c0;
            color: white;
        }
        
        .tag {
            color: #ce9178;
            font-weight: bold;
        }
        
        .message {
            color: #d4d4d4;
        }
        
        .no-logs {
            text-align: center;
            padding: 40px;
            color: #808080;
            font-style: italic;
        }
        
        .auto-refresh {
            display: flex;
            align-items: center;
            gap: 10px;
        }
        
        .auto-refresh input[type="checkbox"] {
            width: 18px;
            height: 18px;
        }
        
        @media (max-width: 768px) {
            .controls {
                flex-direction: column;
                align-items: stretch;
            }
            
            .control-group {
                width: 100%;
            }
            
            .stats {
                flex-direction: column;
                gap: 15px;
            }
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>📋 Log Viewer</h1>
        
        <div class="controls">
            <div class="control-group">
                <label for="level-filter">Level Filter:</label>
                <select id="level-filter">
                    <option value="">All Levels</option>
                    <option value="INFO">INFO</option>
                    <option value="DEBUG">DEBUG</option>
                    <option value="WARNING">WARNING</option>
                    <option value="ERROR">ERROR</option>
                    <option value="SOCKET">SOCKET</option>
                </select>
            </div>
            
            <div class="control-group">
                <label for="tag-filter">Tag Filter:</label>
                <input type="text" id="tag-filter" placeholder="Enter tag name...">
            </div>
            
            <div class="control-group">
                <label for="message-filter">Message Filter:</label>
                <input type="text" id="message-filter" placeholder="Search in messages...">
            </div>
            
            <div class="control-group">
                <label for="sort-order">Sort Order:</label>
                <select id="sort-order">
                    <option value="newest">Newest First</option>
                    <option value="oldest">Oldest First</option>
                </select>
            </div>
            
            <div class="control-group">
                <label for="limit">Limit:</label>
                <select id="limit">
                    <option value="100">100 entries</option>
                    <option value="500">500 entries</option>
                    <option value="1000">1000 entries</option>
                    <option value="0">All entries</option>
                </select>
            </div>
            
            <div class="auto-refresh">
                <input type="checkbox" id="auto-refresh">
                <label for="auto-refresh">Auto Refresh (5s)</label>
            </div>
            
            <button onclick="refreshLogs()">🔄 Refresh</button>
        </div>
        
        <div class="stats" id="stats">
            <div class="stat-item">
                <div class="stat-number" id="total-logs">0</div>
                <div class="stat-label">Total Logs</div>
            </div>
            <div class="stat-item">
                <div class="stat-number" id="filtered-logs">0</div>
                <div class="stat-label">Filtered Logs</div>
            </div>
            <div class="stat-item">
                <div class="stat-number" id="error-count">0</div>
                <div class="stat-label">Errors</div>
            </div>
            <div class="stat-item">
                <div class="stat-number" id="warning-count">0</div>
                <div class="stat-label">Warnings</div>
            </div>
        </div>
        
        <div class="log-container">
            <div class="log-header">
                <span>📄 Log Entries</span>
                <span id="last-updated">Last updated: Never</span>
            </div>
            <div class="log-content" id="log-content">
                <div class="no-logs">Loading logs...</div>
            </div>
        </div>
    </div>

    <script>
        let autoRefreshInterval = null;
        let allLogs = [];
        
        // Log entry parsing regex
        const logRegex = /^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\] \[([^\]]+)\](?:\[([^\]]+)\])? (.+)$/;
        
        function parseLogEntry(line) {
            const match = line.match(logRegex);
            if (!match) return null;
            
            return {
                timestamp: match[1],
                level: match[2],
                tag: match[3] || null,
                message: match[4]
            };
        }
        
        function formatLogEntry(entry) {
            const timestampSpan = `<span class="timestamp">${entry.timestamp}</span>`;
            const levelSpan = `<span class="level ${entry.level}">${entry.level}</span>`;
            const tagSpan = entry.tag ? `<span class="tag">[${entry.tag}]</span>` : '';
            const messageSpan = `<span class="message">${escapeHtml(entry.message)}</span>`;
            
            return `<div class="log-entry">${timestampSpan} ${levelSpan}${tagSpan} ${messageSpan}</div>`;
        }
        
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
        
        function updateStats(logs, filteredLogs) {
            document.getElementById('total-logs').textContent = logs.length;
            document.getElementById('filtered-logs').textContent = filteredLogs.length;
            
            const errorCount = filteredLogs.filter(log => log.level === 'ERROR').length;
            const warningCount = filteredLogs.filter(log => log.level === 'WARNING').length;
            
            document.getElementById('error-count').textContent = errorCount;
            document.getElementById('warning-count').textContent = warningCount;
        }
        
        function filterLogs(logs) {
            const levelFilter = document.getElementById('level-filter').value;
            const tagFilter = document.getElementById('tag-filter').value.toLowerCase();
            const messageFilter = document.getElementById('message-filter').value.toLowerCase();
            
            return logs.filter(log => {
                if (levelFilter && log.level !== levelFilter) return false;
                if (tagFilter && (!log.tag || !log.tag.toLowerCase().includes(tagFilter))) return false;
                if (messageFilter && !log.message.toLowerCase().includes(messageFilter)) return false;
                return true;
            });
        }
        
        function sortLogs(logs) {
            const sortOrder = document.getElementById('sort-order').value;
            return [...logs].sort((a, b) => {
                if (sortOrder === 'newest') {
                    return new Date(b.timestamp) - new Date(a.timestamp);
                } else {
                    return new Date(a.timestamp) - new Date(b.timestamp);
                }
            });
        }
        
        function limitLogs(logs) {
            const limit = parseInt(document.getElementById('limit').value);
            if (limit === 0) return logs;
            return logs.slice(0, limit);
        }
        
        function displayLogs(logs) {
            const logContent = document.getElementById('log-content');
            
            if (logs.length === 0) {
                logContent.innerHTML = '<div class="no-logs">No logs match the current filters.</div>';
                return;
            }
            
            const html = logs.map(formatLogEntry).join('');
            logContent.innerHTML = html;
        }
        
        function refreshLogs() {
            fetch('log_viewer.php?action=get_logs')
                .then(response => response.text())
                .then(data => {
                    const lines = data.split('\n').filter(line => line.trim());
                    allLogs = lines.map(parseLogEntry).filter(entry => entry !== null);
                    
                    const filteredLogs = filterLogs(allLogs);
                    const sortedLogs = sortLogs(filteredLogs);
                    const limitedLogs = limitLogs(sortedLogs);
                    
                    updateStats(allLogs, filteredLogs);
                    displayLogs(limitedLogs);
                    
                    document.getElementById('last-updated').textContent = 
                        `Last updated: ${new Date().toLocaleTimeString()}`;
                })
                .catch(error => {
                    console.error('Error fetching logs:', error);
                    document.getElementById('log-content').innerHTML = 
                        '<div class="no-logs">Error loading logs. Make sure app.log file exists and is readable.</div>';
                });
        }
        
        function toggleAutoRefresh() {
            const checkbox = document.getElementById('auto-refresh');
            
            if (checkbox.checked) {
                autoRefreshInterval = setInterval(refreshLogs, 5000);
            } else {
                if (autoRefreshInterval) {
                    clearInterval(autoRefreshInterval);
                    autoRefreshInterval = null;
                }
            }
        }
        
        // Event listeners
        document.getElementById('level-filter').addEventListener('change', () => {
            const filteredLogs = filterLogs(allLogs);
            const sortedLogs = sortLogs(filteredLogs);
            const limitedLogs = limitLogs(sortedLogs);
            updateStats(allLogs, filteredLogs);
            displayLogs(limitedLogs);
        });
        
        document.getElementById('tag-filter').addEventListener('input', () => {
            const filteredLogs = filterLogs(allLogs);
            const sortedLogs = sortLogs(filteredLogs);
            const limitedLogs = limitLogs(sortedLogs);
            updateStats(allLogs, filteredLogs);
            displayLogs(limitedLogs);
        });
        
        document.getElementById('message-filter').addEventListener('input', () => {
            const filteredLogs = filterLogs(allLogs);
            const sortedLogs = sortLogs(filteredLogs);
            const limitedLogs = limitLogs(sortedLogs);
            updateStats(allLogs, filteredLogs);
            displayLogs(limitedLogs);
        });
        
        document.getElementById('sort-order').addEventListener('change', () => {
            const filteredLogs = filterLogs(allLogs);
            const sortedLogs = sortLogs(filteredLogs);
            const limitedLogs = limitLogs(sortedLogs);
            displayLogs(limitedLogs);
        });
        
        document.getElementById('limit').addEventListener('change', () => {
            const filteredLogs = filterLogs(allLogs);
            const sortedLogs = sortLogs(filteredLogs);
            const limitedLogs = limitLogs(sortedLogs);
            displayLogs(limitedLogs);
        });
        
        document.getElementById('auto-refresh').addEventListener('change', toggleAutoRefresh);
        
        // Initial load
        refreshLogs();
    </script>
</body>
</html>

<?php
// Handle AJAX requests
if (isset($_GET['action'])) {
    header('Content-Type: text/plain');
    
    if ($_GET['action'] === 'get_logs') {
        $logFile = 'app.log';
        if (file_exists($logFile)) {
            echo file_get_contents($logFile);
        } else {
            echo '';
        }
    }
    exit;
}
?>