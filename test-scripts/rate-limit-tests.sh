#!/bin/bash

API_GATEWAY="http://localhost:5000"

echo "=== Rate Limiting Tests ==="

# Test 1: Check rate limit info
echo "1. Getting rate limit configuration..."
curl -s "$API_GATEWAY/api/ratelimit/info" | jq .

echo -e "\n2. Testing registration rate limit (should allow 5 per hour)..."
for i in {1..7}; do
    echo "Registration attempt $i:"
    response=$(curl -s -w "HTTP_STATUS:%{http_code}" \
        -X POST "$API_GATEWAY/api/players/register" \
        -H "Content-Type: application/json" \
        -d "{\"username\":\"testuser$i\",\"email\":\"test$i@example.com\",\"password\":\"password123\"}")
    
    http_status=$(echo "$response" | grep -o "HTTP_STATUS:[0-9]*" | cut -d: -f2)
    body=$(echo "$response" | sed 's/HTTP_STATUS:[0-9]*$//')
    
    echo "Status: $http_status"
    if [ "$http_status" -eq 429 ]; then
        echo "Rate limited! Response: $body"
        break
    fi
    sleep 1
done

echo -e "\n3. Testing login rate limit..."
# First register a user
curl -s -X POST "$API_GATEWAY/api/players/register" \
    -H "Content-Type: application/json" \
    -d '{"username":"logintest","email":"logintest@example.com","password":"password123"}' > /dev/null

# Test login rate limit (10 per 15 minutes)
for i in {1..12}; do
    echo "Login attempt $i:"
    response=$(curl -s -w "HTTP_STATUS:%{http_code}" \
        -X POST "$API_GATEWAY/api/players/login" \
        -H "Content-Type: application/json" \
        -d '{"username":"logintest","password":"wrongpassword"}')
    
    http_status=$(echo "$response" | grep -o "HTTP_STATUS:[0-9]*" | cut -d: -f2)
    echo "Status: $http_status"
    
    if [ "$http_status" -eq 429 ]; then
        echo "Rate limited!"
        break
    fi
    sleep 1
done

echo -e "\n4. Testing authenticated endpoints..."
# Get a valid token
TOKEN_RESPONSE=$(curl -s -X POST "$API_GATEWAY/api/players/login" \
    -H "Content-Type: application/json" \
    -d '{"username":"logintest","password":"password123"}')

TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.accessToken // empty')

if [ -n "$TOKEN" ] && [ "$TOKEN" != "null" ]; then
    echo "Got authentication token"
    
    # Check rate limit status
    echo "Checking rate limit status..."
    curl -s -H "Authorization: Bearer $TOKEN" \
        "$API_GATEWAY/api/ratelimit/status" | jq .
    
    # Test game click rate limit
    echo -e "\nTesting game click rate limit (1000 per minute)..."
    for i in {1..1005}; do
        if [ $((i % 100)) -eq 0 ]; then
            echo "Click $i/1005"
        fi
        
        response=$(curl -s -w "HTTP_STATUS:%{http_code}" \
            -X POST "$API_GATEWAY/api/game/click" \
            -H "Authorization: Bearer $TOKEN" \
            -H "Content-Type: application/json" \
            -d '{"clickPower":1}')
        
        http_status=$(echo "$response" | grep -o "HTTP_STATUS:[0-9]*" | cut -d: -f2)
        
        if [ "$http_status" -eq 429 ]; then
            echo "Rate limited at click $i!"
            body=$(echo "$response" | sed 's/HTTP_STATUS:[0-9]*$//')
            echo "Response: $body"
            break
        fi
    done
else
    echo "Failed to get authentication token"
fi

echo -e "\n=== Rate Limiting Tests Complete ==="