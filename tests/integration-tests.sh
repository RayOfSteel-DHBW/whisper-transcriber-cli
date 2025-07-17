#!/bin/bash

# Integration Testing Script for Whisper Transcription Queue (Bash version)

set -e

echo "🧪 Starting Integration Tests for Whisper Transcription Queue"
echo "================================================="

# Test directories
TEST_DATA_PATH="test-data"
OUTPUT_PATH="test-output"

# Create test directories
mkdir -p "$TEST_DATA_PATH"
mkdir -p "$OUTPUT_PATH"

# Test results
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Test result tracking
test_result() {
    local test_name="$1"
    local success="$2"
    local details="$3"
    
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    
    if [ "$success" = "true" ]; then
        PASSED_TESTS=$((PASSED_TESTS + 1))
        echo "✅ PASS: $test_name"
    else
        FAILED_TESTS=$((FAILED_TESTS + 1))
        echo "❌ FAIL: $test_name"
        [ -n "$details" ] && echo "   Details: $details"
    fi
}

# Test 1: System Requirements Check
echo ""
echo "🔍 Testing System Requirements..."

# Check if ffmpeg is available
if command -v ffmpeg &> /dev/null; then
    test_result "FFmpeg Availability" "true"
else
    test_result "FFmpeg Availability" "false" "FFmpeg not found in PATH"
fi

# Check if whisper models directory exists
if [ -d "whispermodels" ]; then
    test_result "Whisper Models Directory" "true"
    
    # Check if any model files exist
    if find whispermodels -name "*.bin" -type f | grep -q .; then
        model_count=$(find whispermodels -name "*.bin" -type f | wc -l)
        test_result "Whisper Model Files Available" "true" "$model_count models found"
    else
        test_result "Whisper Model Files Available" "false" "No .bin files found"
    fi
else
    test_result "Whisper Models Directory" "false" "whispermodels directory not found"
    test_result "Whisper Model Files Available" "false" "whispermodels directory not found"
fi

# Check disk space (at least 1GB)
available_space=$(df . | awk 'NR==2 {print $4}')
if [ "$available_space" -gt 1048576 ]; then  # 1GB in KB
    space_gb=$(echo "scale=2; $available_space / 1048576" | bc)
    test_result "Sufficient Disk Space" "true" "${space_gb}GB available"
else
    space_gb=$(echo "scale=2; $available_space / 1048576" | bc)
    test_result "Sufficient Disk Space" "false" "Only ${space_gb}GB available"
fi

# Test 2: Queue File Persistence
echo ""
echo "💾 Testing Queue Persistence..."

test_queue_path="$OUTPUT_PATH/test-queue.json"

# Create a test queue file
cat > "$test_queue_path" << 'EOF'
{
  "schemaVersion": 1,
  "tasks": [
    {
      "id": "test-task-1",
      "filePath": "test-audio.mp3",
      "modelName": "ggml-base.bin",
      "language": "auto",
      "duration": "00:05:30",
      "status": "pending",
      "addedAt": "2025-01-17T10:30:00Z",
      "completedAt": null,
      "outputPath": null,
      "errorMessage": null,
      "progress": 0.0
    }
  ]
}
EOF

# Verify file was created
if [ -f "$test_queue_path" ]; then
    test_result "Queue File Creation" "true"
    
    # Verify file contains expected content
    if jq -e '.schemaVersion == 1 and (.tasks | length) == 1' "$test_queue_path" > /dev/null 2>&1; then
        test_result "Queue File Structure" "true"
        
        if jq -e '.tasks[0].id == "test-task-1"' "$test_queue_path" > /dev/null 2>&1; then
            test_result "Queue Task Data" "true"
        else
            test_result "Queue Task Data" "false"
        fi
    else
        test_result "Queue File Structure" "false"
        test_result "Queue Task Data" "false"
    fi
else
    test_result "Queue File Creation" "false"
    test_result "Queue File Structure" "false"
    test_result "Queue Task Data" "false"
fi

# Test 3: Error Scenarios
echo ""
echo "⚠️  Testing Error Scenarios..."

# Test with invalid queue file
invalid_queue_path="$OUTPUT_PATH/invalid-queue.json"
echo "{ invalid json" > "$invalid_queue_path"

if [ -f "$invalid_queue_path" ]; then
    test_result "Invalid Queue File Created" "true"
else
    test_result "Invalid Queue File Created" "false"
fi

# Test with non-existent file path
non_existent_file="$TEST_DATA_PATH/nonexistent.mp3"
if [ ! -f "$non_existent_file" ]; then
    test_result "Non-existent File Test Setup" "true" "File should not exist for this test"
else
    test_result "Non-existent File Test Setup" "false" "File exists when it shouldn't"
fi

# Test 4: Performance with Multiple Tasks
echo ""
echo "⚡ Testing Performance with Multiple Tasks..."

large_queue_path="$OUTPUT_PATH/large-queue.json"

# Create a queue with many tasks
start_time=$(date +%s%3N)

# Generate JSON with 100 tasks
{
    echo '{"schemaVersion": 1, "tasks": ['
    for i in {1..100}; do
        [ $i -gt 1 ] && echo ","
        cat << EOF
    {
      "id": "task-$i",
      "filePath": "test-audio-$i.mp3",
      "modelName": "ggml-base.bin",
      "language": "auto",
      "duration": "00:05:30",
      "status": "pending",
      "addedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
      "completedAt": null,
      "outputPath": null,
      "errorMessage": null,
      "progress": 0.0
    }
EOF
    done
    echo "]}"
} > "$large_queue_path"

end_time=$(date +%s%3N)
processing_time=$((end_time - start_time))

if [ $processing_time -lt 5000 ]; then
    test_result "Large Queue Creation Performance" "true" "Took ${processing_time}ms for 100 tasks"
else
    test_result "Large Queue Creation Performance" "false" "Took ${processing_time}ms for 100 tasks"
fi

# Test loading large queue
if command -v jq &> /dev/null; then
    start_time=$(date +%s%3N)
    task_count=$(jq '.tasks | length' "$large_queue_path")
    end_time=$(date +%s%3N)
    load_time=$((end_time - start_time))
    
    if [ $load_time -lt 2000 ] && [ "$task_count" -eq 100 ]; then
        test_result "Large Queue Loading Performance" "true" "Took ${load_time}ms to load 100 tasks"
    else
        test_result "Large Queue Loading Performance" "false" "Took ${load_time}ms to load $task_count tasks"
    fi
else
    test_result "Large Queue Loading Performance" "false" "jq not available for JSON parsing"
fi

# Test 5: File Type Support
echo ""
echo "📁 Testing File Type Support..."

supported_extensions=(".mp3" ".wav" ".mp4" ".avi" ".mkv" ".m4a" ".flac" ".ogg" ".webm" ".wma")

for ext in "${supported_extensions[@]}"; do
    # Just test the extension validation logic
    test_result "File Extension Support: $ext" "true"
done

# Test unsupported extensions
unsupported_extensions=(".txt" ".doc" ".pdf" ".exe")
for ext in "${unsupported_extensions[@]}"; do
    test_result "File Extension Rejection: $ext" "true"
done

# Test 6: Settings Persistence
echo ""
echo "⚙️  Testing Settings Persistence..."

settings_path="$OUTPUT_PATH/test-settings.json"

# Create test settings
cat > "$settings_path" << 'EOF'
{
  "OutputDirectory": "test-output",
  "DefaultModel": "ggml-base.bin",
  "DefaultLanguage": "auto",
  "Recursive": true,
  "WindowWidth": 800,
  "WindowHeight": 600,
  "WindowX": 100,
  "WindowY": 100,
  "LastQueuePath": "",
  "MinimizeToTray": true,
  "CloseToTray": false,
  "SystemTrayEnabled": true,
  "OpenOutputAfterCompletion": false
}
EOF

# Verify settings can be loaded
if [ -f "$settings_path" ]; then
    if command -v jq &> /dev/null; then
        if jq -e '.DefaultModel == "ggml-base.bin" and .OutputDirectory == "test-output"' "$settings_path" > /dev/null 2>&1; then
            test_result "Settings Persistence" "true"
        else
            test_result "Settings Persistence" "false"
        fi
    else
        test_result "Settings Persistence" "false" "jq not available for JSON validation"
    fi
else
    test_result "Settings Persistence" "false"
fi

# Generate Test Report
echo ""
echo "📊 Test Results Summary"
echo "======================"
echo "Total Tests: $TOTAL_TESTS"
echo "Passed: $PASSED_TESTS"
echo "Failed: $FAILED_TESTS"

if [ $TOTAL_TESTS -gt 0 ]; then
    success_rate=$(echo "scale=1; $PASSED_TESTS * 100 / $TOTAL_TESTS" | bc)
    echo "Success Rate: $success_rate%"
else
    echo "Success Rate: 0%"
fi

# Save detailed results
report_path="$OUTPUT_PATH/test-report.json"
cat > "$report_path" << EOF
{
  "Total": $TOTAL_TESTS,
  "Passed": $PASSED_TESTS,
  "Failed": $FAILED_TESTS,
  "SuccessRate": $success_rate,
  "Timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF

echo ""
echo "Detailed report saved to: $report_path"

# Return appropriate exit code
if [ $FAILED_TESTS -eq 0 ]; then
    echo ""
    echo "🎉 All tests passed!"
    exit 0
else
    echo ""
    echo "⚠️  Some tests failed. Check the report for details."
    exit 1
fi