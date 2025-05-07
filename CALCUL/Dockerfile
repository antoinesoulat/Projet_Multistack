FROM python:3.8-slim

WORKDIR /app

COPY requirements.txt ./ 
RUN pip install --no-cache-dir -r requirements.txt
RUN apt-get update && apt-get install -y curl

COPY . .

EXPOSE 5001

CMD ["python", "app.py"]
