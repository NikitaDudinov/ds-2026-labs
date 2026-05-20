echo "Starting Valuator System..."
docker-compose up -d
echo "System started!"
echo "App 1: http://localhost:5001"
echo "App 2: http://localhost:5002"
echo "Load Balancer (Nginx): http://localhost:8080"