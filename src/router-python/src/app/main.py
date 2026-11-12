from fastapi import FastAPI

app = FastAPI(title="Enterprise AI Smart Router", version="0.1.0")


@app.get("/health")
async def health():
    return {"service": "router-python", "status": "healthy"}
