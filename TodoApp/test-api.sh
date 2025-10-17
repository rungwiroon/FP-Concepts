#!/bin/bash
# Bash script to test the Todo API

BASE_URL="http://localhost:5000"

echo "Testing Todo API..."
echo ""

# Test 1: Create a todo
echo "1. Creating a new todo..."
CREATE_RESPONSE=$(curl -s -X POST "$BASE_URL/todos" \
  -H "Content-Type: application/json" \
  -d '{"title":"Learn functional programming","description":"Study language-ext v5"}')
echo "Created: $CREATE_RESPONSE"
TODO_ID=$(echo $CREATE_RESPONSE | grep -o '"id":[0-9]*' | grep -o '[0-9]*')
echo ""

# Test 2: List all todos
echo "2. Listing all todos..."
curl -s "$BASE_URL/todos" | jq
echo ""

# Test 3: Get specific todo
echo "3. Getting todo by ID..."
curl -s "$BASE_URL/todos/$TODO_ID" | jq
echo ""

# Test 4: Update todo
echo "4. Updating todo..."
curl -s -X PUT "$BASE_URL/todos/$TODO_ID" \
  -H "Content-Type: application/json" \
  -d '{"title":"Learn functional programming with F#","description":"Study language-ext v5 and F#"}' | jq
echo ""

# Test 5: Toggle completion
echo "5. Toggling completion status..."
curl -s -X PATCH "$BASE_URL/todos/$TODO_ID/toggle" | jq
echo ""

# Test 6: Create another todo
echo "6. Creating another todo..."
CREATE_RESPONSE2=$(curl -s -X POST "$BASE_URL/todos" \
  -H "Content-Type: application/json" \
  -d '{"title":"Build a web app","description":"Using ASP.NET Core"}')
echo "Created: $CREATE_RESPONSE2"
TODO_ID2=$(echo $CREATE_RESPONSE2 | grep -o '"id":[0-9]*' | grep -o '[0-9]*')
echo ""

# Test 7: List all todos again
echo "7. Listing all todos again..."
curl -s "$BASE_URL/todos" | jq
echo ""

# Test 8: Delete a todo
echo "8. Deleting todo..."
curl -s -X DELETE "$BASE_URL/todos/$TODO_ID2"
echo "Deleted todo $TODO_ID2"
echo ""

# Test 9: Final list
echo "9. Final list of todos..."
curl -s "$BASE_URL/todos" | jq
echo ""

echo "All tests completed!"
